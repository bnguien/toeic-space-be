using System.Security.Cryptography;
using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.Infrastructure.Services.Security;

public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    private const int Length = 14;

    // Characters that are easy to confuse when read aloud or copied by hand are left out.
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*-_+=?";

    private static readonly string AllCharacters = Uppercase + Lowercase + Digits + Symbols;

    public string Generate()
    {
        // One character from each class guarantees the password rules; the rest are random.
        var characters = new List<char>
        {
            Pick(Uppercase),
            Pick(Lowercase),
            Pick(Digits),
            Pick(Symbols)
        };

        while (characters.Count < Length)
        {
            characters.Add(Pick(AllCharacters));
        }

        var shuffled = characters.ToArray();
        RandomNumberGenerator.Shuffle(shuffled.AsSpan());

        return new string(shuffled);
    }

    private static char Pick(string alphabet)
        => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
