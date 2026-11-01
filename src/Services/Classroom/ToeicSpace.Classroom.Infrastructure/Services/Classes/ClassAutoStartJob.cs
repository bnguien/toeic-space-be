using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.ChangeClassStatus;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Infrastructure.Services.Classes;

public sealed class ClassAutoStartJob(
    IServiceScopeFactory scopes, TimeProvider clock, TimeZoneInfo classTimeZone,
    ILogger<ClassAutoStartJob> logger) : BackgroundService
{
    // Guid.Empty is reserved as the system actor in Class audit fields.
    public const int CheckIntervalMinutes = 5;

    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), classTimeZone).DateTime);
        IReadOnlyList<Guid> ids;
        await using (var scope = scopes.CreateAsyncScope())
        {
            ids = await scope.ServiceProvider.GetRequiredService<IClassRepository>()
                .GetDraftIdsReadyToStartAsync(today, ct);
        }

        logger.LogDebug("Found {ClassCount} draft classes ready for automatic activation on {Date} ({TimeZone})",
            ids.Count, today, classTimeZone.Id);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            // Isolate tracking and concurrency errors so one class cannot block the rest.
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new ChangeClassStatusCommand(ClassStatus.Active)
                    { ClassId = id, AdminId = Guid.Empty }, ct);
                logger.LogInformation("Class {ClassId} automatically activated for {Date} ({TimeZone})",
                    id, today, classTimeZone.Id);
            }
            catch (ClassRuleException ex)
            {
                logger.LogWarning("Class {ClassId} not automatically activated: {Code}", id, ex.Code);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Automatic activation failed for class {ClassId}", id);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(CheckIntervalMinutes), clock);
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Class automatic activation cycle failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
