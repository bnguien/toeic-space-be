using System.Linq.Expressions;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Extensions;

public static class TestMappingExtensions
{
    public static readonly Expression<Func<ToeicTest, TestDto>> DtoProjection = test => new TestDto(
        test.Id,
        test.Code,
        test.Title,
        test.Description,
        test.Category,
        test.Year,
        test.TotalQuestions,
        test.DurationMinutes,
        test.TotalListeningQuestions,
        test.TotalReadingQuestions,
        test.AudioUrl,
        test.IsActive,
        test.Status,
        test.CreatedAt);

    public static TestDetailDto ToDetailDto(
        this ToeicTest test,
        int passageCount,
        IReadOnlyDictionary<ToeicPart, int> questionCountsByPart)
    {
        var parts = Enum.GetValues<ToeicPart>()
            .Select(part => new TestPartSummaryDto(
                part,
                questionCountsByPart.GetValueOrDefault(part),
                ToeicPartRules.GetStandardQuestionCount(part)))
            .ToList();

        var listeningCount = parts
            .Where(part => ToeicPartRules.GetSection(part.Part) == ToeicSection.Listening)
            .Sum(part => part.QuestionCount);

        var readingCount = parts
            .Where(part => ToeicPartRules.GetSection(part.Part) == ToeicSection.Reading)
            .Sum(part => part.QuestionCount);

        return new TestDetailDto(
            test.Id,
            test.Code,
            test.Title,
            test.Description,
            test.Category,
            test.Year,
            test.TotalQuestions,
            test.DurationMinutes,
            test.TotalListeningQuestions,
            test.TotalReadingQuestions,
            test.AudioUrl,
            test.IsActive,
            test.Status,
            passageCount,
            listeningCount + readingCount,
            listeningCount,
            readingCount,
            parts,
            test.CreatedAt,
            test.UpdatedAt);
    }
}
