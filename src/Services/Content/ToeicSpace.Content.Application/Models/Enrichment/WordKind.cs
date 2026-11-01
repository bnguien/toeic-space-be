namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>Word = 1 token, Phrase = 2–5 tokens, Reject = longer or without a latin letter.</summary>
public enum WordKind { Word, Phrase, Reject }

public static class WordKinds
{
    public static string Key(this WordKind kind) => kind.ToString().ToLowerInvariant();
}
