using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ToeicSpace.BuildingBlocks.Messaging.Events;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Infrastructure.Consumers;

namespace ToeicSpace.Identity.Application.UnitTests.Teachers;

public sealed class TeacherIntegrationOutboxTests
{
    [Fact]
    public async Task Saving_relevant_user_changes_stages_versioned_event_transactionally()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var user = await context.AddUserAsync(role: UserRole.Teacher);
        context.Db.ChangeTracker.Clear();
        var stored = await context.Db.Users.SingleAsync(x => x.Id == user.Id);
        stored.FullName = "Updated Teacher";

        await context.Db.SaveChangesAsync();

        stored.Version.Should().Be(2);
        var outbox = await context.Db.OutboxMessages.OrderBy(x => x.OccurredOn).ToListAsync();
        outbox.Should().HaveCount(4);
        var teacherMessage = outbox.Last(x => x.Type.Contains(nameof(TeacherChangedIntegrationEvent)));
        var message = JsonSerializer.Deserialize<TeacherChangedIntegrationEvent>(teacherMessage.Content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        message.Should().NotBeNull();
        message!.Name.Should().Be("Updated Teacher");
        message.Version.Should().Be(2);
        message.IsEligible.Should().BeTrue();
    }

    [Fact]
    public async Task Synchronization_request_stages_current_snapshots_through_outbox()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var user = await context.AddUserAsync(role: UserRole.Teacher);
        context.Db.ChangeTracker.Clear();

        await new TeacherSynchronizationRequestedConsumer(context.Db).SynchronizeAsync();

        var messages = await context.Db.OutboxMessages.ToListAsync();
        messages.Should().HaveCount(3);
        messages.Should().Contain(x => x.Content.Contains(user.Id.ToString(), StringComparison.OrdinalIgnoreCase));
    }
}
