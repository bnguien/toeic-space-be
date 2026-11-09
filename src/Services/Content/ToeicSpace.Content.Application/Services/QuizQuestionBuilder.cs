using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Services;

/// <summary>
/// Builds a "meaning → pick the word" question: the answer plus up to three distractors taken from the candidate
/// pools in priority order. Options stay distinct case-insensitively: never a repeated headword, never a word whose
/// meaning equals the prompt or another option's meaning. Words carry the learner's effective (personal) values.
/// </summary>
public static class QuizQuestionBuilder
{
    public const int OptionCount = 4;

    public static QuizQuestionDto Build(VocabularyWord answer, IEnumerable<IEnumerable<VocabularyWord>> pools,
        Random random)
    {
        var usedHeadwords = new HashSet<string> { NormalizeHeadword(answer) };
        var usedMeanings = new HashSet<string> { NormalizeMeaning(answer.MeaningVi) };
        var distractors = new List<VocabularyWord>(OptionCount - 1);

        foreach (var pool in pools)
        {
            foreach (var candidate in Shuffle(pool.ToArray(), random))
            {
                if (distractors.Count == OptionCount - 1) break;
                if (candidate.Id == answer.Id || usedHeadwords.Contains(NormalizeHeadword(candidate))
                    || usedMeanings.Contains(NormalizeMeaning(candidate.MeaningVi))) continue;
                usedHeadwords.Add(NormalizeHeadword(candidate));
                usedMeanings.Add(NormalizeMeaning(candidate.MeaningVi));
                distractors.Add(candidate);
            }
            if (distractors.Count == OptionCount - 1) break;
        }

        var options = Shuffle(distractors.Append(answer).ToArray(), random)
            .Select(x => new QuizOptionDto(x.Id, x.Headword)).ToArray();
        return new QuizQuestionDto(answer.Id, answer.MeaningVi, answer.PartOfSpeech, options);
    }

    /// <summary>Candidates that would be accepted as distractors for <paramref name="answer"/> (same rules as Build).</summary>
    public static int CountEligible(VocabularyWord answer, IEnumerable<VocabularyWord> candidates) =>
        candidates.Where(x => x.Id != answer.Id && NormalizeHeadword(x) != NormalizeHeadword(answer)
                              && NormalizeMeaning(x.MeaningVi) != NormalizeMeaning(answer.MeaningVi))
            .DistinctBy(NormalizeHeadword).DistinctBy(x => NormalizeMeaning(x.MeaningVi)).Count();

    private static string NormalizeHeadword(VocabularyWord word) => VocabularyWord.Normalize(word.Headword);

    private static string NormalizeMeaning(string meaning) =>
        string.Join(' ', meaning.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static T[] Shuffle<T>(T[] items, Random random)
    {
        random.Shuffle(items);
        return items;
    }
}
