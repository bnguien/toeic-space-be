using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Entities;

namespace ToeicSpace.Identity.Infrastructure.Services.Security;

/// <summary>
/// Issues short-lived ES256 access tokens. Tokens carry only the user id and role:
/// no email or name, because JWT payloads are readable by anyone holding the token.
/// </summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer, IDisposable
{
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

    public void Dispose() => _signingKey.Dispose();
}
