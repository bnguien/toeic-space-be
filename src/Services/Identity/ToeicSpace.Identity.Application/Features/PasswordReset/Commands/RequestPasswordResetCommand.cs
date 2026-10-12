namespace ToeicSpace.Identity.Application.Features.PasswordReset.Commands;

public sealed record RequestPasswordResetCommand(string Email) : IRequest;
