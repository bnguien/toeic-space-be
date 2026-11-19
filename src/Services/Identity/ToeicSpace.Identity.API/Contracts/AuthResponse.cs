using System.Text.Json.Serialization;
using ToeicSpace.Identity.Application.Models;

namespace ToeicSpace.Identity.API.Contracts;

/// <summary>
/// Returned by login and refresh. For browsers the refresh token travels only in the HttpOnly cookie;
/// native clients (see <see cref="Security.NativeClient"/>) receive it in the body instead.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset ExpiresAt,
    AuthenticatedUser User,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? RefreshToken = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? RefreshTokenExpiresAt = null)
{
    public override string ToString() => nameof(AuthResponse);
}
