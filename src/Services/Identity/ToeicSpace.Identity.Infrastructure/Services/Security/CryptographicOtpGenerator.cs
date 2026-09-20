using System.Globalization;
using System.Security.Cryptography;
using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.Infrastructure.Services.Security;

public sealed class CryptographicOtpGenerator : IOtpGenerator
{
    public string Generate()
        => RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);
}
