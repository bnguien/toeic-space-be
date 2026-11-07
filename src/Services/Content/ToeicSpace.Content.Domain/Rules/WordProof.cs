using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ToeicSpace.Content.Domain.Rules;

public sealed record WordProofData(string Algorithm, int Iterations, string Salt, IReadOnlyList<string> Digests);

/// <summary>
/// Word proofs (Studychill <c>wordproof</c>): the client lights words up while typing without ever receiving the
/// answer. Every answer word, normalised like grading, is hashed one way with PBKDF2-HMAC-SHA256, salted per
/// sentence and position (salt ‖ 2-byte big-endian index), 16 bytes. Recovering the text needs a dictionary attack
/// per position. The salt derives from the sentence id, its last update and a server secret, so proofs can be
/// cached per sentence and change when the sentence changes.
/// </summary>
public static class WordProof
{
    public const string Algorithm = "PBKDF2-SHA256";
    public const int KeyLength = 16;
    public const int DefaultIterations = 20_000;

    public static byte[] Salt(byte[] secret, int itemId, DateTime updatedAt)
    {
        var stamp = updatedAt.ToString("O", CultureInfo.InvariantCulture);
        var message = Encoding.UTF8.GetBytes($"dictation-proof-v1:{itemId}:{stamp}");
        return HMACSHA256.HashData(secret, message)[..16];
    }

    public static byte[] Digest(string normalizedWord, byte[] salt, int index, int iterations)
    {
        var salted = new byte[salt.Length + 2];
        salt.CopyTo(salted, 0);
        salted[^2] = (byte)((index >> 8) & 0xff);
        salted[^1] = (byte)(index & 0xff);
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(normalizedWord), salted, iterations,
            HashAlgorithmName.SHA256, KeyLength);
    }

    /// <summary>Proof of every token of <see cref="DictationGrader.Tokens"/> (the list the skeleton exposes).</summary>
    public static WordProofData Build(string text, byte[] salt, int iterations) =>
        new(Algorithm, iterations, Convert.ToBase64String(salt),
            DictationGrader.Tokens(text)
                .Select((token, i) => Convert.ToBase64String(Digest(DictationGrader.Normalize(token), salt, i, iterations)))
                .ToArray());
}
