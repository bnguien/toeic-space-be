using ToeicSpace.Identity.Application.Features.Profile.Commands;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.UnitTests.Profile;

public sealed class UpdateProfileHandlerTests
{
    [Fact]
    public async Task Handle_WhenValidCommand_ShouldUpdateDatabaseAndReturnResult()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var user = await context.AddUserAsync();
        
        var handler = new UpdateProfileHandler(context.Users);
        
        var command = new UpdateProfileCommand(
            "Nguyen Van A",
            "0987654321",
            "https://cdn.toeicspace.vn/avatar.jpg",
            new DateOnly(2000, 1, 1),
            UserGender.Male,
            "Quyet tam dat 990 TOEIC!",
            990,
            EnglishLevel.Advanced
        )
        {
            UserId = user.Id 
        };
        
        var result = await handler.Handle(command, CancellationToken.None);
        
        result.Should().NotBeNull();
        result.FullName.Should().Be("Nguyen Van A");
        result.TargetScore.Should().Be(990);
        
        var updatedUser = await context.Db.Users.FindAsync(user.Id);
        updatedUser!.Phone.Should().Be("0987654321");
        updatedUser.Biography.Should().Be("Quyet tam dat 990 TOEIC!");
    }
    
    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldThrowAppException()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var handler = new UpdateProfileHandler(context.Users);
        
        var command = new UpdateProfileCommand("Hack", null, null, null, null, null, null, null) 
        { 
            UserId = Guid.NewGuid() 
        };
        
        var action = async () => await handler.Handle(command, CancellationToken.None);
        
        var exception = (await action.Should().ThrowAsync<AppException>()).Which;
        exception.Type.Should().Be(ErrorType.NotFound);
    }
}