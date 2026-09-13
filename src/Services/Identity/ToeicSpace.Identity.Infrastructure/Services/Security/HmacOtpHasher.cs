using System.Security.Cryptography;
using System.Text;
using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.Infrastructure.Services.Security;

public sealed class HmacOtpHasher : IOtpHasher
{
    private readonly byte[] _key;

    public HmacOtpHasher(OtpOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HmacSecret) ||
            Encoding.UTF8.GetByteCount(options.HmacSecret) < 32)
        {
            throw new InvalidOperationException(
                "Otp:HmacSecret must contain at least 32 UTF-8 bytes.");
        }

        _key = Encoding.UTF8.GetBytes(options.HmacSecret);
    }

    public string Hash(string otp)
    {
        var otpBytes = Encoding.UTF8.GetBytes(otp);
        var hash = HMACSHA256.HashData(_key, otpBytes);

        return Convert.ToBase64String(hash);
    }

    public bool Verify(string otp, string expectedHash)
    {
        byte[] expectedHashBytes;

        try
        {
            expectedHashBytes = Convert.FromBase64String(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHashBytes = Convert.FromBase64String(Hash(otp));

        return CryptographicOperations.FixedTimeEquals(
            actualHashBytes,
            expectedHashBytes);
    }
}
