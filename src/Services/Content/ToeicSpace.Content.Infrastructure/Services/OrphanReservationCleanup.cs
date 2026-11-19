using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Infrastructure.Persistence;

namespace ToeicSpace.Content.Infrastructure.Services;

public sealed class OrphanReservationCleanup(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<OrphanReservationCleanup> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ReconcileAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError(ex, "Reservation cleanup failed; will retry."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config["Payment:BaseUrl"]) ||
            string.IsNullOrWhiteSpace(config["InternalApi:Key"])) return;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("PaymentStatus");
        var enrollments = scope.ServiceProvider.GetRequiredService<ICourseEnrollmentService>();
        var promotions = scope.ServiceProvider.GetRequiredService<ICoursePromotionService>();
        var references = await db.CourseSlotReservations.AsNoTracking().Where(x =>
                x.Status == SlotReservationStatus.Reserved && x.ExpiresAt < DateTime.UtcNow.AddSeconds(-30))
            .OrderBy(x => x.ExpiresAt).Select(x => x.CheckoutReference).Take(50).ToListAsync(ct);
        foreach (var reference in references)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/internal/orders/by-checkout/{reference}");
            request.Headers.Add("X-Internal-Key", config["InternalApi:Key"]);
            try
            {
                using var response = await http.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    await enrollments.ReleaseAsync(reference, ct);
                else if (response.IsSuccessStatusCode)
                {
                    var order = await response.Content.ReadFromJsonAsync<PaymentOrderState>(cancellationToken: ct);
                    if (order?.Status is "Expired" or "Cancelled") await enrollments.ReleaseAsync(reference, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { log.LogWarning("Reservation {Reference} cleanup deferred: {Type}", reference, ex.GetType().Name); }
        }
        var promotionReferences = await db.PromotionReservations.AsNoTracking().Where(x =>
                x.Status == PromotionReservationStatus.Reserved && x.ExpiresAt < DateTime.UtcNow.AddSeconds(-30))
            .OrderBy(x => x.ExpiresAt).Select(x => x.CheckoutReference).Take(50).ToListAsync(ct);
        foreach (var reference in promotionReferences)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/internal/orders/by-checkout/{reference}");
            request.Headers.Add("X-Internal-Key", config["InternalApi:Key"]);
            try
            {
                using var response = await http.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    await promotions.ReleaseAsync(reference, ct);
                else if (response.IsSuccessStatusCode)
                {
                    var order = await response.Content.ReadFromJsonAsync<PaymentOrderState>(cancellationToken: ct);
                    if (order?.Status is "Expired" or "Cancelled") await promotions.ReleaseAsync(reference, ct);
                    else if (order?.Status == "Paid") await promotions.ConfirmAsync(reference, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { log.LogWarning("Promotion reservation reconciliation deferred: {Type}", ex.GetType().Name); }
        }
    }

    private sealed record PaymentOrderState(string Status, DateTime? PaidAt);
}
