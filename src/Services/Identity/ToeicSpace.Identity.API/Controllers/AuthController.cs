using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using ToeicSpace.Identity.Application.Features.ResendVerification.Commands;
using ToeicSpace.Identity.Application.Features.Register.Commands;
using ToeicSpace.Identity.Application.Features.VerifyEmail.Commands;
using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICookieService _cookieService;

    public AuthController(
        IMediator mediator,
        ICookieService cookieService)
    {
        _mediator = mediator;
        _cookieService = cookieService;
    }

    [HttpPost("register")]
    [SwaggerOperation(
        Summary = "Register a new user",
        Description = "Registers a new user and creates an email verification challenge.")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }

    [HttpPost("verify-email")]
    [SwaggerOperation(
        Summary = "Verify a user's email",
        Description = "Verifies a user's email using an active OTP challenge.")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }

    [HttpPost("resend-verification")]
    [SwaggerOperation(
        Summary = "Resend an email verification OTP",
        Description = "Creates a new verification challenge and requests another OTP email.")]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ResendVerificationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }
}
