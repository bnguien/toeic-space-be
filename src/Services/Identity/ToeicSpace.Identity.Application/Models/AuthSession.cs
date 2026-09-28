namespace ToeicSpace.Identity.Application.Models;

/// <summary>
/// Tokens for a signed-in user. The API layer returns the access token in the body
/// and keeps the refresh token in an HttpOnly cookie; it never goes into a response body.
/// </summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthenticatedUser User);

public sealed record AuthenticatedUser(
    Guid Id,
    string FullName,
    string Email,
    string Role);

public sealed record IssuedAccessToken(
    string Token,
    DateTimeOffset ExpiresAt);
