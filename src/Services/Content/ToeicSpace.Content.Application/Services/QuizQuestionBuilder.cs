using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Services;

/// <summary>
/// Builds a "meaning → pick the word" question: the answer plus up to three distractors taken from the candidate
/// pools in priority order, never repeating a headword and never using a word whose meaning equals the prompt.
/// </summary>
public static class QuizQuestionBuilder
{
    public const int OptionCount = 4;

    public static QuizQuestionDto Build(VocabularyWord answer, IEnumerable<IEnumerable<VocabularyWord>> pools,
        Random random)
    {
        var prompt = NormalizeMeaning(answer.MeaningVi);
        var usedHeadwords = new HashSet<string> { answer.NormalizedHeadword };
        var distractors = new List<VocabularyWord>(OptionCount - 1);

        foreach (var pool in pools)
        {
            foreach (var candidate in Shuffle(pool.ToArray(), random))
            {
                if (distractors.Count == OptionCount - 1) break;
                if (candidate.Id == answer.Id || usedHeadwords.Contains(candidate.NormalizedHeadword)
                    || NormalizeMeaning(candidate.MeaningVi) == prompt) continue;
                usedHeadwords.Add(candidate.NormalizedHeadword);
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
        candidates.Where(x => x.Id != answer.Id && x.NormalizedHeadword != answer.NormalizedHeadword
                              && NormalizeMeaning(x.MeaningVi) != NormalizeMeaning(answer.MeaningVi))
            .Select(x => x.NormalizedHeadword).Distinct().Count();

    private static string NormalizeMeaning(string meaning) => meaning.Trim().ToLowerInvariant();

    private static T[] Shuffle<T>(T[] items, Random random)
    {
        random.Shuffle(items);
        return items;
    }
}
