using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToeicSpace.Content.Application.Interfaces.Services;

namespace ToeicSpace.Content.Infrastructure.Services.Grammar;

public sealed class GrammarOptions
{
    public const string SectionName = "Grammar";

    /// <summary>
    /// Base64 HMAC key (≥ 32 bytes) signing guest attempt tickets. Set it (env <c>Grammar__GuestTokenKey</c>) when the
    /// Content API runs on several instances; without it each process uses a random key (tickets end on restart).
    /// </summary>
    public string? GuestTokenKey { get; set; }
}

/// <summary>Guest attempt tickets: <c>g.</c> + base64url(payload) + "." + base64url(HMAC-SHA256), valid 6 hours.</summary>
public sealed class HmacGuestAttemptTokens : IGuestAttemptTokens
{
    private readonly byte[] _key;
    private readonly TimeProvider _clock;

    public HmacGuestAttemptTokens(IOptions<GrammarOptions> options, TimeProvider clock, ILogger<HmacGuestAttemptTokens> logger)
    {
        _clock = clock;
        var configured = options.Value.GuestTokenKey;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _key = Convert.FromBase64String(configured);
            if (_key.Length < 32) throw new InvalidOperationException("Grammar:GuestTokenKey must be at least 32 bytes.");
        }
        else
        {
            _key = RandomNumberGenerator.GetBytes(32);
            logger.LogWarning("Grammar:GuestTokenKey is not configured; guest attempt tickets use a per-process key.");
        }
    }

    private sealed record Payload(string? T, string? S, string K, long E);

    public string Issue(GuestAttemptRef attempt)
    {
        var expires = _clock.GetUtcNow().Add(IGuestAttemptTokens.Lifetime).ToUnixTimeSeconds();
        var body = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Payload(attempt.Topic, attempt.Stage, attempt.Block, expires)));
        return $"{IGuestAttemptTokens.Prefix}{body}.{Base64Url(Sign(body))}";
    }

    public GuestAttemptRef? Read(string token)
    {
        if (!token.StartsWith(IGuestAttemptTokens.Prefix, StringComparison.Ordinal) || token.Length > 600) return null;
        var parts = token[IGuestAttemptTokens.Prefix.Length..].Split('.');
        if (parts.Length != 2) return null;
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Sign(parts[0]), FromBase64Url(parts[1]))) return null;
            var payload = JsonSerializer.Deserialize<Payload>(FromBase64Url(parts[0]));
            if (payload is null || payload.E < _clock.GetUtcNow().ToUnixTimeSeconds()) return null;
            return new GuestAttemptRef(payload.T, payload.S, payload.K);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private byte[] Sign(string body) => HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(body));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
