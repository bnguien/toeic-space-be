using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ToeicSpace.BuildingBlocks.Security.Extensions;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Identity.Application.UnitTests.Support;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Infrastructure.Services.Security;

namespace ToeicSpace.Identity.Application.UnitTests.Security;

/// <summary>
/// Tokens issued by Identity must pass the validation every other service uses, and nothing else may.
/// </summary>
public sealed class AccessTokenValidationTests
{
    private static readonly User Admin = new()
    {
        Id = Guid.NewGuid(),
        Email = "admin@toeicspace.vn",
        FullName = "Admin",
        Role = UserRole.Admin
    };

    [Fact]
    public async Task Issued_token_is_accepted_with_only_the_public_key()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var token = context.AccessTokenIssuer.Issue(Admin, DateTimeOffset.UtcNow).Token;

        var result = await ValidateAsync(context, token);

        result.IsValid.Should().BeTrue(result.Exception?.Message);
        result.Claims["sub"].Should().Be(Admin.Id.ToString());
        result.Claims["role"].Should().Be("Admin");
        result.Claims.Should().NotContainKeys("email", "name");

        var header = new JsonWebToken(token);
        header.Alg.Should().Be("ES256");
        header.Typ.Should().Be("at+jwt");
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var token = context.AccessTokenIssuer.Issue(Admin, DateTimeOffset.UtcNow.AddMinutes(-11)).Token;

        (await ValidateAsync(context, token)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var forger = new JwtAccessTokenIssuer(new JwtOptions
        {
            Issuer = context.Issuer,
            Audience = context.Audience,
            PrivateKey = Convert.ToBase64String(otherKey.ExportPkcs8PrivateKey())
        });

        var token = forger.Issue(Admin, DateTimeOffset.UtcNow).Token;

        (await ValidateAsync(context, token)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Symmetric_token_signed_with_the_public_key_bytes_is_rejected()
    {
        await using var context = await IdentityTestContext.CreateAsync();

        // Classic algorithm-confusion attack: HS256 keyed with the public key.
        var token = CreateToken(
            context,
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(context.PublicKey)),
                SecurityAlgorithms.HmacSha256),
            AccessTokenDefaults.TokenType);

        (await ValidateAsync(context, token)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Token_with_another_type_is_rejected()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(context.PrivateKey), out _);

        var token = CreateToken(
            context,
            new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256),
            tokenType: "JWT");

        (await ValidateAsync(context, token)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Token_for_another_audience_is_rejected()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var token = context.AccessTokenIssuer.Issue(Admin, DateTimeOffset.UtcNow).Token;

        (await ValidateAsync(context, token, audience: "another-api")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validating_services_refuse_a_configured_private_key()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var privatePem = "-----BEGIN PRIVATE KEY-----\n" + context.PrivateKey + "\n-----END PRIVATE KEY-----";

        var act = () => EcdsaKeyLoader.LoadPublicKey(privatePem, "Jwt:PublicKey");

        act.Should().Throw<InvalidOperationException>().WithMessage("*private key*");
    }

    [Fact]
    public void Missing_or_non_p256_keys_fail_at_startup()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        FluentActions.Invoking(() => EcdsaKeyLoader.LoadPublicKey("", "Jwt:PublicKey"))
            .Should().Throw<InvalidOperationException>().WithMessage("*not configured*");
        FluentActions.Invoking(() => EcdsaKeyLoader.LoadPublicKey(
                Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo()),
                "Jwt:PublicKey"))
            .Should().Throw<InvalidOperationException>().WithMessage("*P-256*");
        FluentActions.Invoking(() => EcdsaKeyLoader.LoadPrivateKey("not-base64!", "Jwt:PrivateKey"))
            .Should().Throw<InvalidOperationException>();
    }

    private static string CreateToken(
        IdentityTestContext context,
        SigningCredentials credentials,
        string tokenType)
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = context.Issuer,
            Audience = context.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            TokenType = tokenType,
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Admin.Id.ToString(),
                ["role"] = "Admin"
            }
        });

    private static async Task<TokenValidationResult> ValidateAsync(
        IdentityTestContext context,
        string token,
        string? audience = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = context.Issuer,
                ["Jwt:Audience"] = audience ?? context.Audience,
                ["Jwt:PublicKey"] = context.PublicKey
            })
            .Build();

        var services = new ServiceCollection();
        services.AddToeicSpaceJwtAuthentication(configuration);
        await using var provider = services.BuildServiceProvider();

        var parameters = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .TokenValidationParameters;

        return await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
    }
}
