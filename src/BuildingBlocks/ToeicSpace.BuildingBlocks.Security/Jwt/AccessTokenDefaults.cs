using Microsoft.IdentityModel.Tokens;

namespace ToeicSpace.BuildingBlocks.Security.Jwt;

/// <summary>
/// Contract shared by the Identity service (issuer) and every service that validates access tokens.
/// </summary>
public static class AccessTokenDefaults
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Access tokens are signed with ECDSA P-256 so only the Identity service holds the signing key.
    /// </summary>
    public const string Algorithm = SecurityAlgorithms.EcdsaSha256;

    /// <summary>
    /// JWT "typ" header (RFC 9068). Tokens of any other type are rejected.
    /// </summary>
    public const string TokenType = "at+jwt";

    public const string SubjectClaim = "sub";

    public const string RoleClaim = "role";

    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);
}
