using FluentAssertions;
using ToeicSpace.Identity.Domain.Enums;
using Xunit;

namespace ToeicSpace.Identity.Domain.UnitTests;

public class UserRoleTests
{
    [Theory]
    [InlineData(UserRole.User, 1)]
    [InlineData(UserRole.Admin, 2)]
    [InlineData(UserRole.Teacher, 3)]
    public void UserRole_EnumValues_ShouldMatchSpecification(UserRole role, int expectedValue)
    {
        ((int)role).Should().Be(expectedValue);
    }

    [Fact]
    public void UserRole_ShouldIncludeTeacherRole()
    {
        // Assert that Teacher is a recognized enum member with value 3
        Enum.IsDefined(typeof(UserRole), UserRole.Teacher).Should().BeTrue();
        Enum.GetName(typeof(UserRole), 3).Should().Be("Teacher");
    }

    [Theory]
    [InlineData("User", UserRole.User)]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("Teacher", UserRole.Teacher)]
    public void UserRole_ShouldParseCorrectly(string roleName, UserRole expectedRole)
    {
        var success = Enum.TryParse<UserRole>(roleName, true, out var parsedRole);
        success.Should().BeTrue();
        parsedRole.Should().Be(expectedRole);
    }
}
