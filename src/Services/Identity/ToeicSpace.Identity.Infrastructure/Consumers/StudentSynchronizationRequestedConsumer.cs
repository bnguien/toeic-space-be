using MassTransit;
using Microsoft.Extensions.Logging;
using ToeicSpace.BuildingBlocks.Messaging.Events;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Infrastructure.Persistence;

namespace ToeicSpace.Identity.Infrastructure.Consumers;

public sealed class StudentSynchronizationRequestedConsumer(IdentityDbContext db,
    ILogger<StudentSynchronizationRequestedConsumer> logger)
    : IConsumer<StudentSynchronizationRequestedIntegrationEvent>
{
    public Task Consume(ConsumeContext<StudentSynchronizationRequestedIntegrationEvent> context) =>
        SynchronizeAsync(context.CancellationToken);

    public async Task SynchronizeAsync(CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking().ToListAsync(ct);
        foreach (var user in users)
        {
            var deleted = user.DeletedAt is not null;
            db.EnqueueIntegrationEvent(new StudentChangedIntegrationEvent(
                user.Id, user.FullName, user.Email, user.Role.ToString(),
                deleted ? "Deleted" : user.Status.ToString(),
                !deleted && user.EmailVerifiedAt is not null,
                !deleted && user.Role == UserRole.User && user.Status == UserStatus.Active && user.EmailVerifiedAt is not null,
                user.Version));
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Staged {SnapshotCount} student projection snapshots for synchronization", users.Count);
    }
}
