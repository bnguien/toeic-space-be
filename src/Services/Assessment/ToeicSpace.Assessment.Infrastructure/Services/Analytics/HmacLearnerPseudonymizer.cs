using System.Security.Cryptography;
using System.Text;
using ToeicSpace.Assessment.Application.Interfaces.Security;

namespace ToeicSpace.Assessment.Infrastructure.Services.Analytics;

/// <summary>
/// Pseudonym = first 16 lowercase hex characters of HMAC-SHA256(key, id). Without the key a pseudonym
/// cannot be linked back to a user ID, and different deployments produce unrelated pseudonyms.
/// </summary>
public sealed class HmacLearnerPseudonymizer : ILearnerPseudonymizer
{
    private const int PseudonymBytes = 8;

    private readonly byte[]? _key;

    public HmacLearnerPseudonymizer(AnalyticsOptions options)
    {
        var key = options.PseudonymKey;

        _key = string.IsNullOrWhiteSpace(key) || key.Length < AnalyticsOptions.MinimumPseudonymKeyLength
            ? null
            : Encoding.UTF8.GetBytes(key);
    }

    public bool IsConfigured => _key is not null;

    public string Learner(Guid userId)
        => Hash(userId.ToString("D"));

    /// <summary>Prefixed so an attempt pseudonym never equals a learner pseudonym.</summary>
    public string Attempt(Guid attemptId)
        => Hash("attempt:" + attemptId.ToString("D"));

    private string Hash(string value)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("Analytics pseudonym key is not configured.");
        }

        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash, 0, PseudonymBytes);
    }
}
