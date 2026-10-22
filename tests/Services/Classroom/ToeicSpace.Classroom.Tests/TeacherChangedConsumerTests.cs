using Microsoft.EntityFrameworkCore;
using ToeicSpace.BuildingBlocks.Messaging.Events;
using ToeicSpace.Classroom.Infrastructure.Consumers;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ClassroomClass = ToeicSpace.Classroom.Domain.Entities.Class;
using ToeicSpace.Classroom.Domain.Entities;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class TeacherChangedConsumerTests
{
    [Fact]
    public async Task Upserts_projection_and_ignores_duplicate_and_out_of_order_events()
    {
        var options = new DbContextOptionsBuilder<ClassroomDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ClassroomDbContext(options);
        var consumer = new TeacherChangedConsumer(db, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<TeacherChangedConsumer>.Instance);
        var teacherId = Guid.NewGuid();

        await consumer.ApplyAsync(Event(teacherId, 2, "Current", true));
        await consumer.ApplyAsync(Event(teacherId, 1, "Stale", false));
        await consumer.ApplyAsync(Event(teacherId, 2, "Duplicate", false));

        var projection = await db.Teachers.SingleAsync();
        Assert.Equal("Current", projection.Name);
        Assert.True(projection.IsEligible);
        Assert.Equal(2, projection.Version);
    }

    [Fact]
    public async Task Ineligible_event_updates_projection_without_touching_assignment_history()
    {
        var options = new DbContextOptionsBuilder<ClassroomDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ClassroomDbContext(options);
        var teacherId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        db.Classes.Add(new ClassroomClass { Id = classId, Code = "A", Name = "A" });
        db.ClassInstructors.Add(new ClassInstructor { ClassId = classId, TeacherId = teacherId });
        await db.SaveChangesAsync();

        await new TeacherChangedConsumer(db, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<TeacherChangedConsumer>.Instance).ApplyAsync(Event(teacherId, 3, "Teacher", false));

        Assert.Single(await db.ClassInstructors.ToListAsync());
        Assert.False((await db.Teachers.SingleAsync()).IsEligible);
    }

    private static TeacherChangedIntegrationEvent Event(Guid id, long version, string name, bool eligible) =>
        new(id, name, "teacher@example.com", null, eligible ? "Teacher" : "User", eligible ? "Active" : "Inactive", eligible, version);
}
