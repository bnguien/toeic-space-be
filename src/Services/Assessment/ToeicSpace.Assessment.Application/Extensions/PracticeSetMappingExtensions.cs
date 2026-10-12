using System.Linq.Expressions;
using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Extensions;

public static class PracticeSetMappingExtensions
{
    public static readonly Expression<Func<ToeicPracticeSet, PracticeSetDto>> DtoProjection = practiceSet => new PracticeSetDto(
        practiceSet.Id,
        practiceSet.Code,
        practiceSet.Title,
        practiceSet.Description,
        practiceSet.Kind,
        practiceSet.Part,
        practiceSet.Level,
        practiceSet.TargetScore,
        practiceSet.DurationMinutes,
        practiceSet.Status,
        practiceSet.OrderIndex,
        practiceSet.Items.Count,
        practiceSet.CreatedAt);

    public static PracticeSetDetailDto ToDetailDto(
        this ToeicPracticeSet practiceSet,
        IReadOnlyList<PracticeSetItemDto> items)
    {
        return new PracticeSetDetailDto(
            practiceSet.Id,
            practiceSet.Code,
            practiceSet.Title,
            practiceSet.Description,
            practiceSet.Kind,
            practiceSet.Part,
            practiceSet.Level,
            practiceSet.TargetScore,
            practiceSet.DurationMinutes,
            practiceSet.Status,
            practiceSet.OrderIndex,
            items,
            practiceSet.CreatedAt,
            practiceSet.UpdatedAt);
    }
}
