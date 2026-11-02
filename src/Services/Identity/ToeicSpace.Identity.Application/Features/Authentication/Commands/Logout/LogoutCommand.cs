namespace ToeicSpace.Identity.Application.Features.Authentication.Commands.Logout;

public sealed record LogoutCommand(string? RefreshToken) : IRequest
{
    public override string ToString() => nameof(LogoutCommand);
}
