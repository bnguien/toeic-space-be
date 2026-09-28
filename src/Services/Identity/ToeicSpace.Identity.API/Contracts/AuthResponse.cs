using ToeicSpace.Identity.Application.Models;

namespace ToeicSpace.Identity.API.Contracts;

/// <summary>
/// Returned by login and refresh. The refresh token travels only in the HttpOnly cookie.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset ExpiresAt,
    AuthenticatedUser User);
