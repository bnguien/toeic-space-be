using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Dictation;

public sealed record SkeletonWordDto(string Before, int Length, string After);

public sealed record DictationKeywordDto(string Word, string Meaning, string PartOfSpeech, string Ipa, string Cefr);

public sealed record DictationNoteDto(string Id, string Text, DictationNoteType Type, string Explanation);

public sealed record WordProofDto(string Algorithm, int Iterations, string Salt, IReadOnlyList<string> Digests);

/// <summary>Sentence frame (slots + punctuation), meaning, keywords, IPA, notes and word proofs — no answer letters.</summary>
public sealed record DictationSkeletonDto(IReadOnlyList<SkeletonWordDto> Words, int WordCount, string Translation,
    int Level, IReadOnlyList<DictationKeywordDto> Keywords, WordProofDto Proof, string Ipa,
    IReadOnlyList<DictationNoteDto> Notes);
