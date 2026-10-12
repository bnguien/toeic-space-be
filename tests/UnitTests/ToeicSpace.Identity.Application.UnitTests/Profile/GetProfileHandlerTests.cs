using ToeicSpace.Identity.Application.Features.Profile.Queries;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.UnitTests.Profile;

public sealed class GetProfileHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserExists_ShouldReturnUserProfileResult()
    {
        await using var context = await IdentityTestContext.CreateAsync();

        var user = await context.AddUserAsync(email: "student@toeicspace.com");

        var handler = new GetProfileHandler(context.Users);
        var query = new GetProfileQuery(user.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.Email.Should().Be("student@toeicspace.com");
        result.FullName.Should().Be("Test User");
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldThrowAppException()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var handler = new GetProfileHandler(context.Users);

        var query = new GetProfileQuery(Guid.NewGuid());
        var action = async () => await handler.Handle(query, CancellationToken.None);
        var exception = (await action.Should().ThrowAsync<AppException>()).Which;
        exception.Type.Should().Be(ErrorType.NotFound);
    }
}