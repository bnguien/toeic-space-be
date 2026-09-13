using ToeicSpace.Identity.Application.Features.Register.Results;

namespace ToeicSpace.Identity.Application.Features.Register.Commands;

public sealed record RegisterCommand(
    string FullName,
    string Email,
    string? Phone,
    string Password,
    string ConfirmPassword) : IRequest<RegisterResult>;
