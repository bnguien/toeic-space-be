using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.Infrastructure.Services.Security;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenBytes = 32;

    public string Generate()
        => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(TokenBytes));

    // 256 random bits cannot be brute-forced, so a fast hash is enough;
    // it keeps a database leak from exposing usable tokens.
    public string Hash(string refreshToken)
        => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
