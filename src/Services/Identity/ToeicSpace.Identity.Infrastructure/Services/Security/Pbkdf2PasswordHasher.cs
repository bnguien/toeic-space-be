using System.Globalization;
using System.Security.Cryptography;
using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.Infrastructure.Services.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string AlgorithmName = "PBKDF2-SHA512";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MaxIterations = 5_000_000;

    // Used when there is no stored hash, so unknown accounts cost the same time to check.
    private static readonly byte[] DummySalt = RandomNumberGenerator.GetBytes(SaltSize);

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, Iterations, HashSize);

        return string.Join(
            '$',
            AlgorithmName,
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string? passwordHash)
    {
        if (!TryParse(passwordHash, out var iterations, out var salt, out var expectedHash))
        {
            Derive(password, DummySalt, Iterations, HashSize);
            return false;
        }

        var actualHash = Derive(password, salt, iterations, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length)
        => Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA512,
            length);

    private static bool TryParse(
        string? passwordHash,
        out int iterations,
        out byte[] salt,
        out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        var parts = passwordHash?.Split('$');
        if (parts is not { Length: 4 }
            || parts[0] != AlgorithmName
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            || iterations is < 1 or > MaxIterations)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length >= SaltSize && hash.Length >= HashSize;
    }
}
