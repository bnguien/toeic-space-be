using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Infrastructure.Services.Security;

/// <summary>
/// Issues short-lived ES256 access tokens. Tokens carry only the user id and role:
/// no email or name, because JWT payloads are readable by anyone holding the token.
/// </summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer, IDisposable
{
    /// <summary>
    /// Claim of assistant tokens naming who the assistant acts for (from X-On-Behalf-Of). Audit only.
    /// </summary>
    public const string ActorClaim = "act";

    private readonly JwtOptions _options;
    private readonly ECDsa _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public JwtAccessTokenIssuer(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("Configure 'Jwt:Issuer' and 'Jwt:Audience'.");
        }

        if (options.AccessTokenLifetime <= TimeSpan.Zero
            || options.AccessTokenLifetime > TimeSpan.FromMinutes(60))
        {
            throw new InvalidOperationException(
                "'Jwt:AccessTokenLifetime' must be between 00:00:01 and 01:00:00.");
        }

        _options = options;
        _signingKey = EcdsaKeyLoader.LoadPrivateKey(options.PrivateKey, "Jwt:PrivateKey");
        _signingCredentials = new SigningCredentials(
            new ECDsaSecurityKey(_signingKey) { KeyId = EcdsaKeyLoader.ComputeKeyId(_signingKey) },
            AccessTokenDefaults.Algorithm);
    }

    public IssuedAccessToken Issue(User user, DateTimeOffset issuedAt)
    {
        var expiresAt = issuedAt.Add(_options.AccessTokenLifetime);

        var token = _tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            TokenType = AccessTokenDefaults.TokenType,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [AccessTokenDefaults.SubjectClaim] = user.Id.ToString(),
                [AccessTokenDefaults.RoleClaim] = user.Role.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N")
            }
        });

        return new IssuedAccessToken(token, expiresAt);
    }

    public IssuedAccessToken IssueForAgent(Guid subjectId, DateTimeOffset issuedAt, TimeSpan lifetime, string? actor = null)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromMinutes(15))
        {
            lifetime = TimeSpan.FromMinutes(10);
        }

        var claims = new Dictionary<string, object>
        {
            [AccessTokenDefaults.SubjectClaim] = subjectId.ToString(),
            [AccessTokenDefaults.RoleClaim] = UserRole.Admin.ToString(),
            [AccessTokenDefaults.ScopeClaim] = AccessTokenDefaults.AgentReadScope,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N")
        };

        // Audit only: services never authorize on it.
        if (!string.IsNullOrEmpty(actor))
        {
            claims[ActorClaim] = actor;
        }

        var expiresAt = issuedAt.Add(lifetime);
        var token = _tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            TokenType = AccessTokenDefaults.TokenType,
            SigningCredentials = _signingCredentials,
            Claims = claims
        });

        return new IssuedAccessToken(token, expiresAt);
    }

    public void Dispose() => _signingKey.Dispose();
}
