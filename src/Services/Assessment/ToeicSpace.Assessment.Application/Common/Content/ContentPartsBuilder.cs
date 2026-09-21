using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Common.Content;

/// <summary>
/// Arranges passages and questions into the Part 1-7 structure used by tests and practice sets.
/// </summary>
public static class ContentPartsBuilder
{
    /// <param name="passages">Passages referenced by the questions (their Questions list is ignored).</param>
    /// <param name="orderedQuestions">Questions already sorted in display order.</param>
    public static IReadOnlyList<ContentPartDto> Build(
        IReadOnlyCollection<ContentPassageDto> passages,
        IReadOnlyList<ContentQuestionDto> orderedQuestions)
    {
        var passagesById = passages.ToDictionary(passage => passage.Id);
        var questionsByPassage = new Dictionary<Guid, List<ContentQuestionDto>>();
        var passageOrder = new List<Guid>();
        var standaloneQuestions = new List<ContentQuestionDto>();

        foreach (var question in orderedQuestions)
        {
            // A question pointing to a passage that is not part of this content is still shown,
            // as a standalone question, so it can never silently disappear.
            if (question.PassageId is not { } passageId || !passagesById.ContainsKey(passageId))
            {
                standaloneQuestions.Add(question);
                continue;
            }

            if (!questionsByPassage.TryGetValue(passageId, out var groupQuestions))
            {
                groupQuestions = [];
                questionsByPassage[passageId] = groupQuestions;
                passageOrder.Add(passageId);
            }

            groupQuestions.Add(question);
        }

        var groupedPassages = passageOrder
            .Select(passageId => passagesById[passageId] with { Questions = questionsByPassage[passageId] })
            .ToList();

        return Enum.GetValues<ToeicPart>()
            .Select(part =>
            {
                var partPassages = groupedPassages
                    .Where(passage => passage.Part == part)
                    .ToList();

                var partStandaloneQuestions = standaloneQuestions
                    .Where(question => question.Part == part)
                    .ToList();

                return new ContentPartDto(
                    part,
                    $"Part {(int)part}",
                    ToeicPartRules.GetSection(part),
                    partPassages.Sum(passage => passage.Questions.Count) + partStandaloneQuestions.Count,
                    partPassages,
                    partStandaloneQuestions);
            })
            .Where(part => part.QuestionCount > 0)
            .ToList();
    }

    /// <summary>
    /// Removes everything that reveals answers: the answer key, explanations, transcripts,
    /// translations (a Part 6 translation spells out the missing sentences) and the script of
    /// listening passages (Part 3/4 passage content is the conversation itself).
    /// Part 1 and 2 prompts are only heard in the real test, so their text is hidden as well.
    /// </summary>
    public static ContentQuestionDto WithoutAnswers(this ContentQuestionDto question)
        => question with
        {
            QuestionText = question.Part is ToeicPart.Part1 or ToeicPart.Part2 ? null : question.QuestionText,
            CorrectAnswer = null,
            Explanation = null,
            Transcript = null,
            TranscriptTranslation = null
        };

    public static ContentPassageDto WithoutAnswers(this ContentPassageDto passage)
        => ToeicPartRules.GetSection(passage.Part) == ToeicSection.Listening
            ? passage with { Content = null, ContentTranslation = null, Transcript = null }
            : passage with { ContentTranslation = null, Transcript = null };
}
