using System.Security.Cryptography;
using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.Infrastructure.Services.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA512,
            HashSize);

        return string.Join(
            '$',
            "PBKDF2-SHA512",
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }
}
