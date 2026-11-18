using ToeicSpace.Identity.Application.DTOs.Registration;


namespace ToeicSpace.Identity.Application.Features.Registration.Commands.Register;

public sealed record RegisterCommand(
    string FullName,
    string Email,
    string? Phone,
    string Password,
    string ConfirmPassword) : IRequest<RegisterResult>;
