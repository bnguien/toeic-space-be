using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Common.Content;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Interfaces.Caching;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetContent;

public sealed class GetPracticeSetContentHandler : IRequestHandler<GetPracticeSetContentQuery, PracticeSetContentDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IContentCache _cache;

    public GetPracticeSetContentHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser,
        IContentCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<PracticeSetContentDto> Handle(
        GetPracticeSetContentQuery request,
        CancellationToken cancellationToken)
    {
        var canManageContent = _currentUser.CanManageContent;
        var cacheKey = ContentCacheKeys.PracticeSet(request.Id, canManageContent);
        var cached = await _cache.GetAsync<PracticeSetContentDto>(cacheKey, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        var practiceSet = await _context.ToeicPracticeSets
            .AsNoTracking()
            .VisibleTo(canManageContent)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

        var itemQuery = _context.ToeicPracticeSetItems
            .AsNoTracking()
            .Where(item => item.PracticeSetId == practiceSet.Id);

        if (!canManageContent)
        {
            itemQuery = itemQuery.Where(item => item.Question.Status == ContentStatus.Active);
        }

        var questions = await itemQuery
            .OrderBy(item => item.OrderIndex)
            .Select(item => item.Question)
            .Select(ContentProjections.Question)
            .ToListAsync(cancellationToken);

        var passages = await _context.LoadPassagesAsync(questions, cancellationToken);

        var result = new PracticeSetContentDto(
            practiceSet.Id,
            practiceSet.Code,
            practiceSet.Title,
            practiceSet.Description,
            practiceSet.Kind,
            practiceSet.Part,
            practiceSet.Level,
            practiceSet.TargetScore,
            practiceSet.DurationMinutes,
            questions.Count,
            IncludesAnswers: true,
            ContentPartsBuilder.Build(passages, questions));

        await _cache.SetAsync(cacheKey, result, cancellationToken);

        return result;
    }
}
