using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ToeicSpace.Content.Application.Models.Dictation;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Services;

/// <summary>
/// Word proofs of a sentence, cached per (sentence, last update): PBKDF2 with 20 000 rounds costs a few ms per
/// word. Singleton; the cache is cleared when it grows past <see cref="MaxEntries"/>.
/// </summary>
public sealed class DictationProofService(DictationSettings settings)
{
    private const int MaxEntries = 5000;
    private readonly byte[] _secret = string.IsNullOrEmpty(settings.ProofSecret)
        ? RandomNumberGenerator.GetBytes(32)
        : Encoding.UTF8.GetBytes(settings.ProofSecret);
    private readonly ConcurrentDictionary<(int, long), WordProofData> _cache = new();

    public int Iterations => Math.Clamp(settings.ProofIterations, 1000, 200_000);

    public WordProofData For(DictationItem item)
    {
        var key = (item.Id, item.UpdatedAt.Ticks);
        if (_cache.TryGetValue(key, out var cached)) return cached;
        if (_cache.Count >= MaxEntries) _cache.Clear();
        var proof = WordProof.Build(item.Text, WordProof.Salt(_secret, item.Id, item.UpdatedAt), Iterations);
        _cache[key] = proof;
        return proof;
    }
}
