using System.Linq.Expressions;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Common.Content;

public static class ContentProjections
{
    public static readonly Expression<Func<ToeicQuestion, ContentQuestionDto>> Question = question => new ContentQuestionDto(
        question.Id,
        question.PassageId,
        question.Part,
        question.Section,
        question.QuestionNumber,
        question.QuestionText,
        question.AudioUrl,
        question.ImageUrl,
        question.OptionA,
        question.OptionB,
        question.OptionC,
        question.OptionD,
        question.CorrectAnswer,
        question.Explanation,
        TranslatedText.TranscriptText(question.Transcript),
        TranslatedText.TranscriptTranslation(question.Transcript),
        question.DifficultyLevel,
        question.OrderIndex);

    public static readonly Expression<Func<ToeicPassage, ContentPassageDto>> Passage = passage => new ContentPassageDto(
        passage.Id,
        passage.Part,
        passage.PassageType,
        passage.Title,
        TranslatedText.PassageText(passage.Content),
        TranslatedText.PassageTranslation(passage.Content),
        passage.AudioUrl,
        passage.ImageUrl,
        passage.Transcript,
        passage.OrderIndex,
        new List<ContentQuestionDto>());

    /// <summary>
    /// Loads the passages referenced by the questions, wherever they belong.
    /// </summary>
    public static async Task<IReadOnlyList<ContentPassageDto>> LoadPassagesAsync(
        this IAssessmentDbContext context,
        IEnumerable<ContentQuestionDto> questions,
        CancellationToken cancellationToken)
    {
        var passageIds = questions
            .Where(question => question.PassageId.HasValue)
            .Select(question => question.PassageId!.Value)
            .Distinct()
            .ToList();

        if (passageIds.Count == 0)
        {
            return [];
        }

        return await context.ToeicPassages
            .AsNoTracking()
            .Where(passage => passageIds.Contains(passage.Id))
            .Select(Passage)
            .ToListAsync(cancellationToken);
    }
}
