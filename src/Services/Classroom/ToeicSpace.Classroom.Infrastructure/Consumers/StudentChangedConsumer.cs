using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ToeicSpace.BuildingBlocks.Messaging.Events;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Infrastructure.Persistence;

namespace ToeicSpace.Classroom.Infrastructure.Consumers;

public sealed class StudentChangedConsumer(ClassroomDbContext db, TimeProvider clock, ILogger<StudentChangedConsumer> logger)
    : IConsumer<StudentChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<StudentChangedIntegrationEvent> context) =>
        ApplyAsync(context.Message, context.CancellationToken);

    public async Task ApplyAsync(StudentChangedIntegrationEvent message, CancellationToken ct = default)
    {
        var student = await db.Students.FirstOrDefaultAsync(x => x.StudentId == message.StudentId, ct);
        if (student is not null && student.Version >= message.Version)
        {
            logger.LogDebug("Skipped stale student event {EventId} for {StudentId}", message.Id, message.StudentId);
            return;
        }
        if (student is null)
        {
            student = new StudentReadModel { StudentId = message.StudentId };
            db.Students.Add(student);
        }
        student.Name = message.Name;
        student.Email = message.Email;
        student.Role = message.Role;
        student.Status = message.Status;
        student.IsVerified = message.IsVerified;
        student.IsEligible = message.IsEligible;
        student.Version = message.Version;
        student.LastEventId = message.Id;
        student.SynchronizedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Student projection {StudentId} synchronized at version {Version}; role {Role}, status {Status}, eligible {IsEligible}",
            message.StudentId, message.Version, message.Role, message.Status, message.IsEligible);
    }
}

public sealed class StudentChangedConsumerDefinition : ConsumerDefinition<StudentChangedConsumer>
{
    public StudentChangedConsumerDefinition()
    {
        EndpointName = "classroom-student-changed";
        ConcurrentMessageLimit = 1;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpoint, IConsumerConfigurator<StudentChangedConsumer> consumer, IRegistrationContext context) =>
        endpoint.UseMessageRetry(x => x.Interval(5, TimeSpan.FromSeconds(1)));
}
