using Microsoft.EntityFrameworkCore;
using ToeicSpace.Identity.Application.Features.Students.Queries;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.UnitTests.Students;

public sealed class GetStudentsTests
{
    [Fact]
    public async Task Admin_search_returns_only_active_verified_students_with_pagination()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var first = await context.AddUserAsync("one@example.com", UserRole.User);
        await context.AddUserAsync("two@example.com", UserRole.User);
        await context.AddUserAsync("unverified@example.com", UserRole.User, emailVerified: false);
        await context.AddUserAsync("inactive@example.com", UserRole.User, UserStatus.Inactive);
        await context.AddUserAsync("teacher@example.com", UserRole.Teacher);
        var deleted = await context.AddUserAsync("deleted@example.com", UserRole.User);
        (await context.Db.Users.SingleAsync(x => x.Id == deleted.Id)).DeletedAt = DateTime.UtcNow;
        await context.Db.SaveChangesAsync();

        var handler = new GetStudentsHandler(context.Users);
        var result = await handler.Handle(new GetStudentsQuery(null, 1, 1), default);
        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        var secondPage = await handler.Handle(new GetStudentsQuery(null, 2, 1), default);
        Assert.Equal(2, secondPage.TotalCount);
        Assert.NotEqual(result.Items[0].Id, secondPage.Items[0].Id);
        var filtered = await handler.Handle(new GetStudentsQuery(" one@ "), default);
        Assert.Equal(first.Id, Assert.Single(filtered.Items).Id);
        Assert.Equal("Active", filtered.Items[0].Status);
        Assert.Empty((await handler.Handle(new GetStudentsQuery("teacher@"), default)).Items);
    }

    [Fact]
    public void Search_validates_pagination()
    {
        var validator = new GetStudentsValidator();
        Assert.False(validator.Validate(new GetStudentsQuery(null, 0, 20)).IsValid);
        Assert.False(validator.Validate(new GetStudentsQuery(null, 1, 101)).IsValid);
        Assert.False(validator.Validate(new GetStudentsQuery(null, int.MaxValue, 20)).IsValid);
        Assert.True(validator.Validate(new GetStudentsQuery(null)).IsValid);
    }
}
