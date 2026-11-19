using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.BuildingBlocks.Messaging.Events;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Infrastructure.Consumers;

namespace ToeicSpace.Identity.Application.UnitTests.Students;

public sealed class StudentIntegrationOutboxTests
{
    [Fact]
    public async Task Eligibility_changes_are_staged_for_registration_verification_and_inactivation()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var user = await context.AddUserAsync(role: UserRole.User, status: UserStatus.Inactive, emailVerified: false);
        var events = await StudentEventsAsync(context);
        events.Should().ContainSingle().Which.IsEligible.Should().BeFalse();

        var stored = await context.Db.Users.SingleAsync(x => x.Id == user.Id);
        stored.EmailVerifiedAt = DateTime.UtcNow;
        stored.Status = UserStatus.Active;
        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();
        events = await StudentEventsAsync(context);
        events[^1].IsEligible.Should().BeTrue();
        events[^1].IsVerified.Should().BeTrue();

        stored = await context.Db.Users.SingleAsync(x => x.Id == user.Id);
        stored.Status = UserStatus.Suspended;
        await context.Db.SaveChangesAsync();
        events = await StudentEventsAsync(context);
        events[^1].IsEligible.Should().BeFalse();
        events.Select(x => x.Version).Should().BeInAscendingOrder();

        context.Db.ChangeTracker.Clear();
        stored = await context.Db.Users.SingleAsync(x => x.Id == user.Id);
        stored.Role = UserRole.Teacher;
        stored.Status = UserStatus.Active;
        await context.Db.SaveChangesAsync();
        events = await StudentEventsAsync(context);
        events[^1].Role.Should().Be("Teacher");
        events[^1].IsEligible.Should().BeFalse();
    }

    [Fact]
    public async Task Full_sync_stages_student_snapshot_without_reading_another_database()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var user = await context.AddUserAsync(role: UserRole.User);
        await new StudentSynchronizationRequestedConsumer(context.Db,
            NullLogger<StudentSynchronizationRequestedConsumer>.Instance).SynchronizeAsync();
        var events = await StudentEventsAsync(context);
        events.Should().HaveCount(2);
        events[^1].StudentId.Should().Be(user.Id);
        events[^1].IsEligible.Should().BeTrue();
    }

    private static async Task<List<StudentChangedIntegrationEvent>> StudentEventsAsync(IdentityTestContext context)
    {
        var messages = await context.Db.OutboxMessages.AsNoTracking()
            .Where(x => x.Type.Contains(nameof(StudentChangedIntegrationEvent)))
            .OrderBy(x => x.OccurredOn).ThenBy(x => x.Id).ToListAsync();
        return messages.Select(x => JsonSerializer.Deserialize<StudentChangedIntegrationEvent>(
            x.Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!).ToList();
    }
}
