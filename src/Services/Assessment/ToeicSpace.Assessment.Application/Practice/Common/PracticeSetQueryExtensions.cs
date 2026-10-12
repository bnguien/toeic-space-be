using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Practice.Common;

public static class PracticeSetQueryExtensions
{
    public static IQueryable<ToeicPracticeSet> VisibleTo(
        this IQueryable<ToeicPracticeSet> query,
        bool canManageContent)
    {
        return canManageContent
            ? query
            : query.Where(practiceSet => practiceSet.Status == ContentStatus.Active);
    }

    public static async Task<IReadOnlyList<PracticeSetItemDto>> LoadItemsAsync(
        this IAssessmentDbContext context,
        Guid practiceSetId,
        CancellationToken cancellationToken)
    {
        return await context.ToeicPracticeSetItems
            .AsNoTracking()
            .Where(item => item.PracticeSetId == practiceSetId)
            .OrderBy(item => item.OrderIndex)
            .Select(item => new PracticeSetItemDto(item.QuestionId, item.OrderIndex))
            .ToListAsync(cancellationToken);
    }

    public static async Task EnsureCodeIsAvailableAsync(
        this IAssessmentDbContext context,
        string code,
        Guid? currentPracticeSetId,
        CancellationToken cancellationToken)
    {
        var codeTaken = await context.ToeicPracticeSets
            .IgnoreQueryFilters()
            .AnyAsync(practiceSet => practiceSet.Code == code && practiceSet.Id != currentPracticeSetId, cancellationToken);

        if (codeTaken)
        {
            throw AppException.Conflict($"Practice set code '{code}' is already used.", ErrorCodes.DuplicateCode);
        }
    }
}
