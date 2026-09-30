using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Identity.API.Contracts;
using ToeicSpace.Identity.API.Security;
using ToeicSpace.Identity.Application.Features.ChangePassword.Commands;
using ToeicSpace.Identity.Application.Features.PasswordReset.Commands;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Options;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitPolicies.Credentials)]
public sealed class PasswordController(
    IMediator mediator,
    ICookieService cookies,
    RefreshCookieOptions refreshCookie,
    PasswordResetOptions options) : ControllerBase
{
    private const string ResetCookie = "__Secure-ts_pr";
    private const string ResetCookiePath = "/identity/api/v1/auth/password-reset";

    [HttpPost("password-reset/otp")]
    [AllowAnonymous]
    [RequireCsrfHeader]
    public async Task<IActionResult> RequestOtp(
        [FromBody] RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        cookies.Delete(ResetCookie, ResetCookiePath);
        return Ok(new
        {
            Message = "If the account is eligible, a password reset OTP will be sent to its email.",
            CooldownSeconds = options.ResendCooldownSeconds
        });
    }

    [HttpPost("password-reset/verify")]
    [AllowAnonymous]
    [RequireCsrfHeader]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        cookies.Set(ResetCookie, result.Token, result.ExpiresAt, ResetCookiePath);
        return Ok(new { result.ExpiresAt });
    }

    [HttpPost("password-reset/confirm")]
    [AllowAnonymous]
    [RequireCsrfHeader]
    public async Task<IActionResult> Confirm(
        [FromBody] ConfirmPasswordResetRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ConfirmPasswordResetCommand(cookies.Get(ResetCookie) ?? string.Empty,
            request.NewPassword, request.ConfirmPassword), cancellationToken);
        ClearCookies();
        return NoContent();
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> Change(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var userId))
        {
            throw AppException.Unauthenticated(code: ErrorCodes.TokenInvalid);
        }

        await mediator.Send(new ChangePasswordCommand(userId, request.CurrentPassword,
            request.NewPassword, request.ConfirmPassword), cancellationToken);
        ClearCookies();
        return NoContent();
    }

    private void ClearCookies()
    {
        cookies.Delete(ResetCookie, ResetCookiePath);
        cookies.Delete(refreshCookie.Name, refreshCookie.Path);
    }
}
