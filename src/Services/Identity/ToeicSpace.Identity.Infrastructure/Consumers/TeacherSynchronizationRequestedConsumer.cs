using MassTransit;
using ToeicSpace.BuildingBlocks.Messaging.Events;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Infrastructure.Persistence;

namespace ToeicSpace.Identity.Infrastructure.Consumers;

public sealed class TeacherSynchronizationRequestedConsumer(IdentityDbContext db)
    : IConsumer<TeacherSynchronizationRequestedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TeacherSynchronizationRequestedIntegrationEvent> context) =>
        SynchronizeAsync(context.CancellationToken);

    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            var deleted = user.DeletedAt is not null;
            db.EnqueueIntegrationEvent(new TeacherChangedIntegrationEvent(
                user.Id, user.FullName, user.Email, user.AvatarUrl,
                user.Role.ToString(), deleted ? "Deleted" : user.Status.ToString(),
                !deleted && user.Role == UserRole.Teacher && user.Status == UserStatus.Active,
                user.Version));
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
