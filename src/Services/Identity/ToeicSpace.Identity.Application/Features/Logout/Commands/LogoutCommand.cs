namespace ToeicSpace.Identity.Application.Features.Logout.Commands;

public sealed record LogoutCommand(string? RefreshToken) : IRequest
{
    public override string ToString() => nameof(LogoutCommand);
}
