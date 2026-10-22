using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ToeicSpace.BuildingBlocks.Messaging.Events;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Infrastructure.Persistence;

namespace ToeicSpace.Classroom.Infrastructure.Consumers;

public sealed class TeacherChangedConsumer(ClassroomDbContext db, TimeProvider clock, ILogger<TeacherChangedConsumer> logger)
    : IConsumer<TeacherChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TeacherChangedIntegrationEvent> context) =>
        ApplyAsync(context.Message, context.CancellationToken);

    public async Task ApplyAsync(TeacherChangedIntegrationEvent message, CancellationToken ct = default)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(x => x.TeacherId == message.TeacherId, ct);
        if (teacher is not null && teacher.Version >= message.Version)
        {
            logger.LogDebug("Skipped stale teacher event {EventId} for teacher {TeacherId}; incoming version {IncomingVersion}, current version {CurrentVersion}", message.Id, message.TeacherId, message.Version, teacher.Version);
            return;
        }

        if (teacher is null)
        {
            teacher = new TeacherReadModel { TeacherId = message.TeacherId };
            db.Teachers.Add(teacher);
        }

        teacher.Name = message.Name;
        teacher.Email = message.Email;
        teacher.AvatarUrl = message.AvatarUrl;
        teacher.Role = message.Role;
        teacher.Status = message.Status;
        teacher.IsEligible = message.IsEligible;
        teacher.Version = message.Version;
        teacher.LastEventId = message.Id;
        teacher.SynchronizedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Teacher projection {TeacherId} synchronized at version {Version} from event {EventId}", message.TeacherId, message.Version, message.Id);
    }
}

public sealed class TeacherChangedConsumerDefinition : ConsumerDefinition<TeacherChangedConsumer>
{
    public TeacherChangedConsumerDefinition()
    {
        EndpointName = "classroom-teacher-changed";
        ConcurrentMessageLimit = 1;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpoint, IConsumerConfigurator<TeacherChangedConsumer> consumer, IRegistrationContext context) =>
        endpoint.UseMessageRetry(x => x.Interval(5, TimeSpan.FromSeconds(1)));
}
