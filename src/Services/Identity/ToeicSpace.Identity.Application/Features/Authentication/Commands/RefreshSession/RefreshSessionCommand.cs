using ToeicSpace.Identity.Application.Models;

namespace ToeicSpace.Identity.Application.Features.Authentication.Commands.RefreshSession;

public sealed record RefreshSessionCommand(string RefreshToken) : IRequest<AuthSession>
{
    public override string ToString() => nameof(RefreshSessionCommand);
}
