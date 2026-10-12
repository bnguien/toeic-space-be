using ToeicSpace.Identity.Application.Features.Register.Commands;

namespace ToeicSpace.Identity.Application.Tests;

public sealed class RegisterValidatorTests
{
    private readonly RegisterValidator _validator = new();

    [Fact]
    public void Validate_WithStrongPassword_IsValid()
    {
        var result = _validator.Validate(CreateCommand("Password1!"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Pass1!", "at least 8 characters")]
    [InlineData("password1!", "uppercase letter")]
    [InlineData("Password!", "digit")]
    [InlineData("Password1", "special character")]
    [InlineData("Password1 ", "special character")]
    public void Validate_WithWeakPassword_ReturnsExpectedError(
        string password,
        string expectedMessage)
    {
        var result = _validator.Validate(CreateCommand(password));

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(RegisterCommand.Password) &&
                     error.ErrorMessage.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase));
    }

    private static RegisterCommand CreateCommand(string password)
        => new(
            "Ada Lovelace",
            "ada@example.com",
            null,
            password,
            password);
}
