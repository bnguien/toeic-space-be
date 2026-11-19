using ToeicSpace.Payment.Application.DTOs.Orders;
using ToeicSpace.Payment.Domain.Exceptions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ToeicSpace.Payment.Infrastructure.Services.Content;

public sealed class ContentCheckoutClient(HttpClient http, IConfiguration config)
{
    private void Authenticate()
    {
        var key = config["InternalApi:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException("InternalApi:Key must be configured in both services.");
        http.DefaultRequestHeaders.Remove("X-Internal-Key");
        http.DefaultRequestHeaders.Add("X-Internal-Key", key);
    }

    public async Task<CourseCheckout> CourseAsync(Guid courseId, Guid userId, CancellationToken ct)
    {
        Authenticate();
        using var response = await http.GetAsync($"api/v1/internal/checkout/courses/{courseId}?userId={userId}", ct);
        await EnsureAsync(response, ct);
        var course = await response.Content.ReadFromJsonAsync<CourseCheckout>(cancellationToken: ct);
        if (course is null || course.CourseId != courseId)
            throw new PaymentException("content_invalid", "Content returned an invalid course.", 503);
        return course;
    }

    public async Task<CourseCheckout> ReserveAsync(Guid courseId, Guid userId, Guid reference, CancellationToken ct)
    {
        Authenticate();
        using var response = await http.PostAsJsonAsync("api/v1/internal/checkout/reservations",
            new { courseId, userId, checkoutReference = reference }, ct);
        await EnsureAsync(response, ct);
        var course = await response.Content.ReadFromJsonAsync<CourseCheckout>(cancellationToken: ct);
        if (course is null || course.CourseId != courseId ||
            (course.ReservationId is null) != (course.ExpiresAt is null))
            throw new PaymentException("content_invalid", "Content returned an invalid course.", 503);
        // MySQL datetime has no timezone. Content stores UTC, but JSON serialized from a
        // database read may omit Z. Preserve the UTC instant rather than interpreting it as local.
        return course with { ExpiresAt = course.ExpiresAt is { } expiry ? AsUtc(expiry) : null };
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public async Task ReleaseAsync(Guid reference, CancellationToken ct)
    {
        Authenticate();
        using var response = await http.PostAsync($"api/v1/internal/checkout/reservations/{reference}/release", null, ct);
        await EnsureAsync(response, ct);
    }

    public async Task<PromotionQuote?> PromotionAsync(Guid courseId, CancellationToken ct)
    {
        Authenticate();
        using var response = await http.GetAsync($"api/v1/internal/checkout/promotions/{courseId}", ct);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<PromotionQuote>(cancellationToken: ct);
    }

    public async Task<PromotionQuote> ReservePromotionAsync(Guid courseId, Guid promotionId, Guid reference,
        decimal originalPrice, DateTime expiresAt, CancellationToken ct)
    {
        Authenticate();
        using var response = await http.PostAsJsonAsync("api/v1/internal/checkout/promotions/reservations",
            new { courseId, promotionId, checkoutReference = reference, originalPrice, expiresAt }, ct);
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<PromotionQuote>(cancellationToken: ct)
            ?? throw new PaymentException("content_invalid", "Content returned no promotion reservation.", 503);
    }

    public async Task SettlePromotionAsync(Guid reference, bool paid, CancellationToken ct)
    {
        Authenticate();
        using var response = await http.PostAsync($"api/v1/internal/checkout/promotions/reservations/{reference}/" +
            (paid ? "confirm" : "release"), null, ct);
        await EnsureAsync(response, ct);
    }

    public async Task FulfillAsync(Guid courseId, Guid userId, Guid reference, DateTime paidAt,
        bool requiresReservation, CancellationToken ct)
    {
        Authenticate();
        using var response = await http.PostAsJsonAsync("api/v1/internal/checkout/fulfill",
            new { courseId, userId, checkoutReference = reference, paidAt, requiresReservation }, ct);
        await EnsureAsync(response, ct);
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? code = null;
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            try
            {
                using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (body.RootElement.TryGetProperty("code", out var value)) code = value.GetString();
            }
            catch (JsonException) { }
        }
        throw response.StatusCode switch
        {
            HttpStatusCode.Conflict when code == "concurrency_conflict" =>
                new PaymentException("content_unavailable", "Concurrent Content update; retry.", 503),
            HttpStatusCode.Conflict => new PaymentException("course_unavailable", "Course checkout or reservation is unavailable."),
            HttpStatusCode.NotFound => new PaymentException("course_not_found", "Course not found.", 404),
            _ => new PaymentException("content_unavailable", "Content Service is unavailable.", 503)
        };
    }
}
