using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Identity.API.Contracts;
using ToeicSpace.Identity.API.Security;
using ToeicSpace.Identity.Application.Features.ChangePassword.Commands;
using ToeicSpace.Identity.Application.Features.CurrentUser.Queries;
using ToeicSpace.Identity.Application.Features.Login.Commands;
using ToeicSpace.Identity.Application.Features.Logout.Commands;
using ToeicSpace.Identity.Application.Features.PasswordReset.Commands;
using ToeicSpace.Identity.Application.Features.RefreshSession.Commands;
using ToeicSpace.Identity.Application.Features.ResendVerification.Commands;
using ToeicSpace.Identity.Application.Features.Register.Commands;
using ToeicSpace.Identity.Application.Features.VerifyEmail.Commands;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Application.Options;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitPolicies.Credentials)]
public sealed class AuthController : ControllerBase
{
    private const string BearerTokenType = "Bearer";
    private const string ResetCookie = "__Secure-ts_pr";
    private const string ResetCookiePath = "/identity/api/v1/auth/password-reset";

    private readonly IMediator _mediator;
    private readonly ICookieService _cookieService;
    private readonly RefreshCookieOptions _refreshCookie;
    private readonly TimeProvider _timeProvider;
    private readonly PasswordResetOptions _passwordResetOptions;

    public AuthController(
        IMediator mediator,
        ICookieService cookieService,
        RefreshCookieOptions refreshCookie,
        TimeProvider timeProvider,
        PasswordResetOptions passwordResetOptions)
    {
        _mediator = mediator;
        _cookieService = cookieService;
        _refreshCookie = refreshCookie;
        _timeProvider = timeProvider;
        _passwordResetOptions = passwordResetOptions;
    }

    [HttpPost("register")]
    [AllowAnonymous]
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
    [AllowAnonymous]
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
    [AllowAnonymous]
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

    [HttpPost("login")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Sign in",
        Description = "Returns a short-lived access token and sets the refresh token as an HttpOnly cookie. " +
            "Repeated failures lock the email address temporarily.")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var session = await _mediator.Send(command, cancellationToken);

        return Ok(StartSession(session));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [RequireCsrfHeader]
    [EnableRateLimiting(RateLimitPolicies.Session)]
    [SwaggerOperation(
        Summary = "Refresh the session",
        Description = $"Rotates the refresh cookie and returns a new access token. Requires the {RequireCsrfHeaderAttribute.HeaderName} header. " +
            "Reusing an old refresh token ends every session of the account.")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = _cookieService.Get(_refreshCookie.Name);

        if (string.IsNullOrEmpty(refreshToken))
        {
            return SessionEnded(ErrorCodes.TokenInvalid);
        }

        AuthSession session;

        try
        {
            session = await _mediator.Send(new RefreshSessionCommand(refreshToken), cancellationToken);
        }
        catch (AppException exception) when (exception.Type is ErrorType.Unauthenticated or ErrorType.Validation)
        {
            // The exception handler clears response headers, so the stale cookie is removed here.
            return SessionEnded(exception.Code ?? ErrorCodes.TokenInvalid);
        }

        return Ok(StartSession(session));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [RequireCsrfHeader]
    [EnableRateLimiting(RateLimitPolicies.Session)]
    [SwaggerOperation(
        Summary = "Sign out",
        Description = "Revokes the current refresh token and clears the cookie.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new LogoutCommand(_cookieService.Get(_refreshCookie.Name)),
            cancellationToken);

        _cookieService.Delete(_refreshCookie.Name, _refreshCookie.Path);

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Session)]
    [SwaggerOperation(
        Summary = "Get the signed-in user",
        Description = "Profile and role of the access token owner, read from the database.")]
    [ProducesResponseType(typeof(AuthenticatedUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var userId))
        {
            return SessionEnded(ErrorCodes.TokenInvalid, clearCookie: false);
        }

        var user = await _mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);

        return Ok(user);
    }

    // Preserve the password routes used by the frontend and the reset cookie path.
    [HttpPost("~/api/v1/auth/password-reset/otp")]
    [AllowAnonymous]
    [RequireCsrfHeader]
    public async Task<IActionResult> RequestOtp(
        [FromBody] RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        _cookieService.Delete(ResetCookie, ResetCookiePath);
        return Ok(new
        {
            Message = "If the account is eligible, a password reset OTP will be sent to its email.",
            CooldownSeconds = _passwordResetOptions.ResendCooldownSeconds
        });
    }

    [HttpPost("~/api/v1/auth/password-reset/verify")]
    [AllowAnonymous]
    [RequireCsrfHeader]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        _cookieService.Set(ResetCookie, result.Token, result.ExpiresAt, ResetCookiePath);
        return Ok(new { result.ExpiresAt });
    }

    [HttpPost("~/api/v1/auth/password-reset/confirm")]
    [AllowAnonymous]
    [RequireCsrfHeader]
    public async Task<IActionResult> Confirm(
        [FromBody] ConfirmPasswordResetRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new ConfirmPasswordResetCommand(_cookieService.Get(ResetCookie) ?? string.Empty,
            request.NewPassword, request.ConfirmPassword), cancellationToken);
        ClearCookies();
        return NoContent();
    }

    [HttpPost("~/api/v1/auth/change-password/otp")]
    [Authorize]
    public async Task<IActionResult> RequestChangeOtp(
        [FromBody] RequestChangePasswordOtpRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var userId))
        {
            throw AppException.Unauthenticated(code: ErrorCodes.TokenInvalid);
        }

        await _mediator.Send(new RequestChangePasswordOtpCommand(userId, request.CurrentPassword),
            cancellationToken);
        return Ok(new { CooldownSeconds = _passwordResetOptions.ResendCooldownSeconds });
    }

    [HttpPut("~/api/v1/auth/change-password")]
    [Authorize]
    public async Task<IActionResult> Change(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var userId))
        {
            throw AppException.Unauthenticated(code: ErrorCodes.TokenInvalid);
        }

        await _mediator.Send(new ChangePasswordCommand(userId, request.CurrentPassword,
            request.NewPassword, request.ConfirmPassword, request.Otp), cancellationToken);
        ClearCookies();
        return NoContent();
    }

    private void ClearCookies()
    {
        _cookieService.Delete(ResetCookie, ResetCookiePath);
        _cookieService.Delete(_refreshCookie.Name, _refreshCookie.Path);
    }

    private AuthResponse StartSession(AuthSession session)
    {
        _cookieService.Set(
            _refreshCookie.Name,
            session.RefreshToken,
            session.RefreshTokenExpiresAt,
            _refreshCookie.Path);

        var expiresIn = (int)Math.Max(
            0,
            (session.AccessTokenExpiresAt - _timeProvider.GetUtcNow()).TotalSeconds);

        return new AuthResponse(
            session.AccessToken,
            BearerTokenType,
            expiresIn,
            session.AccessTokenExpiresAt,
            session.User);
    }

    private ObjectResult SessionEnded(string code, bool clearCookie = true)
    {
        if (clearCookie)
        {
            _cookieService.Delete(_refreshCookie.Name, _refreshCookie.Path);
        }

        return new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Your session has ended. Please sign in again.",
            Instance = HttpContext.Request.Path,
            Extensions = { ["code"] = code }
        })
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            ContentTypes = { "application/problem+json" }
        };
    }
}
