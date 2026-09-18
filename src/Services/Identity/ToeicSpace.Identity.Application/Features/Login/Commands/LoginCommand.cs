using ToeicSpace.Identity.Application.Models;

namespace ToeicSpace.Identity.Application.Features.Login.Commands;

public sealed record LoginCommand(
    string Email,
    string Password) : IRequest<AuthSession>
{
    // Keep the password out of logs if the command is ever serialized or printed.
    public override string ToString() => $"{nameof(LoginCommand)} {{ Email = {Email} }}";
}
