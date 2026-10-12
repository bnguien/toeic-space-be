using System.Linq.Expressions;
using ToeicSpace.Assessment.Application.Common.Content;
using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Extensions;

public static class QuestionMappingExtensions
{
    /// <summary>
    /// Server-side projection so list queries never load long text columns.
    /// </summary>
    public static readonly Expression<Func<ToeicQuestion, QuestionDto>> DtoProjection = question => new QuestionDto(
        question.Id,
        question.TestId,
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
        question.DifficultyLevel,
        question.Topic,
        question.Status,
        question.Version,
        question.OrderIndex);

    private static readonly Func<ToeicQuestion, QuestionDto> CompiledDtoProjection = DtoProjection.Compile();

    public static QuestionDto ToDto(this ToeicQuestion question)
        => CompiledDtoProjection(question);

    public static QuestionDetailDto ToDetailDto(
        this ToeicQuestion question,
        IReadOnlyList<Guid> practiceSetIds)
    {
        return new QuestionDetailDto(
            question.Id,
            question.TestId,
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
            question.Topic,
            question.Status,
            question.Version,
            question.OrderIndex,
            question.PreferAiExplanation,
            practiceSetIds,
            question.CreatedAt,
            question.UpdatedAt);
    }
}
