using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Interfaces.Caching;

namespace ToeicSpace.Assessment.Application.Common.Caching;

public sealed class ContentCacheInvalidator
{
    private readonly IAssessmentDbContext _context;
    private readonly IContentCache _cache;

    public ContentCacheInvalidator(
        IAssessmentDbContext context,
        IContentCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public Task InvalidateTestsAsync(
        IEnumerable<Guid?> testIds,
        CancellationToken cancellationToken)
    {
        var keys = testIds
            .OfType<Guid>()
            .Distinct()
            .SelectMany(ContentCacheKeys.AllFullTestVariants);

        return _cache.RemoveAsync(keys, cancellationToken);
    }

    public Task InvalidatePracticeSetsAsync(
        IEnumerable<Guid> practiceSetIds,
        CancellationToken cancellationToken)
    {
        var keys = practiceSetIds
            .Distinct()
            .SelectMany(ContentCacheKeys.AllPracticeSetVariants);

        return _cache.RemoveAsync(keys, cancellationToken);
    }

    /// <summary>
    /// Invalidates every test and practice set that renders the given questions.
    /// </summary>
    public async Task InvalidateQuestionsAsync(
        IReadOnlyCollection<Guid> questionIds,
        IEnumerable<Guid?> testIds,
        CancellationToken cancellationToken)
    {
        var practiceSetIds = await _context.ToeicPracticeSetItems
            .AsNoTracking()
            .Where(item => questionIds.Contains(item.QuestionId))
            .Select(item => item.PracticeSetId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await InvalidateTestsAsync(testIds, cancellationToken);
        await InvalidatePracticeSetsAsync(practiceSetIds, cancellationToken);
    }
}
