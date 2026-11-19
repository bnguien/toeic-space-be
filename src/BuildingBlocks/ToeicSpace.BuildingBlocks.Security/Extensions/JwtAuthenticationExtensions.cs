using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ToeicSpace.BuildingBlocks.Security.Jwt;

namespace ToeicSpace.BuildingBlocks.Security.Extensions;

public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Validates ES256 access tokens issued by the Identity service.
    /// Reads Jwt:Issuer, Jwt:Audience and Jwt:PublicKey unless a validation key is passed in.
    /// </summary>
    public static AuthenticationBuilder AddToeicSpaceJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        ECDsa? validationKey = null)
    {
        var section = configuration.GetSection(AccessTokenDefaults.SectionName);
        var issuer = section["Issuer"];
        var audience = section["Audience"];

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "JWT settings are missing. Configure 'Jwt:Issuer' and 'Jwt:Audience'.");
        }

        var publicKey = validationKey is null
            ? EcdsaKeyLoader.LoadPublicKey(section["PublicKey"], "Jwt:PublicKey")
            : EcdsaKeyLoader.ToPublicKey(validationKey);

        var signingKey = new ECDsaSecurityKey(publicKey)
        {
            KeyId = EcdsaKeyLoader.ComputeKeyId(publicKey)
        };

        return services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                // Do not tell callers why a token was rejected.
                options.IncludeErrorDetails = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidAlgorithms = [AccessTokenDefaults.Algorithm],
                    ValidTypes = [AccessTokenDefaults.TokenType],
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = AccessTokenDefaults.ClockSkew,
                    NameClaimType = AccessTokenDefaults.SubjectClaim,
                    RoleClaimType = AccessTokenDefaults.RoleClaim
                };

                options.Events = new JwtBearerEvents
                {
                    // AI assistant tokens (scope agent.read) may only read.
                    OnTokenValidated = context =>
                    {
                        var scope = context.Principal?.FindFirst(AccessTokenDefaults.ScopeClaim)?.Value;
                        var method = context.HttpContext.Request.Method;
                        if (scope == AccessTokenDefaults.AgentReadScope
                            && !HttpMethods.IsGet(method)
                            && !HttpMethods.IsHead(method))
                        {
                            context.Fail("Assistant tokens are read-only.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });
    }
}
