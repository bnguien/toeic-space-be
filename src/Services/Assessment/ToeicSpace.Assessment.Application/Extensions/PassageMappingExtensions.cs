using System.Linq.Expressions;
using ToeicSpace.Assessment.Application.Common.Content;
using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Extensions;

public static class PassageMappingExtensions
{
    public static readonly Expression<Func<ToeicPassage, PassageDto>> DtoProjection = passage => new PassageDto(
        passage.Id,
        passage.TestId,
        passage.Part,
        passage.PassageType,
        passage.Title,
        TranslatedText.PassageText(passage.Content),
        TranslatedText.PassageTranslation(passage.Content),
        passage.AudioUrl,
        passage.ImageUrl,
        passage.Transcript,
        passage.OrderIndex,
        passage.Questions.Count(question => question.DeletedAt == null),
        passage.CreatedAt,
        passage.UpdatedAt);

    public static PassageDetailDto ToDetailDto(
        this ToeicPassage passage,
        IReadOnlyList<QuestionDto> questions)
    {
        return new PassageDetailDto(
            passage.Id,
            passage.TestId,
            passage.Part,
            passage.PassageType,
            passage.Title,
            TranslatedText.PassageText(passage.Content),
            TranslatedText.PassageTranslation(passage.Content),
            passage.AudioUrl,
            passage.ImageUrl,
            passage.Transcript,
            passage.OrderIndex,
            questions,
            passage.CreatedAt,
            passage.UpdatedAt);
    }
}
