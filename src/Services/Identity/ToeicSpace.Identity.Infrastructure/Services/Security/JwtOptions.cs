using ToeicSpace.BuildingBlocks.Security.Jwt;

namespace ToeicSpace.Identity.Infrastructure.Services.Security;

public sealed class JwtOptions
{
    public const string SectionName = AccessTokenDefaults.SectionName;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// P-256 private key (PEM or base64 PKCS#8). Only the Identity service may hold it.
    /// </summary>
    public string PrivateKey { get; init; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(10);
}
