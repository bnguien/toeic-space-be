namespace ToeicSpace.Identity.Application.Features.Passwords.Commands.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : IRequest;
