using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Common.Content;
using ToeicSpace.Assessment.Application.Common.Querying;
using ToeicSpace.Assessment.Application.DTOs.Content;
using ToeicSpace.Assessment.Application.DTOs.PracticeSets;
using ToeicSpace.Assessment.Application.Interfaces.Caching;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Interfaces.Security;

namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Queries.GetPracticeSetContent;

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
        if (!_currentUser.IsAuthenticated)
        {
            throw new AppException(ErrorType.Unauthenticated, "Sign in to view practice question content.");
        }

        var canManageContent = _currentUser.CanManageContent;
        // Visibility is checked even on cache hits, so cached content cannot reveal an archived set.
        var practiceSet = await _context.ToeicPracticeSets
            .AsNoTracking()
            .VisibleTo(canManageContent)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

        var cacheKey = ContentCacheKeys.PracticeSet(request.Id, canManageContent);
        var cached = await _cache.GetAsync<PracticeSetContentDto>(cacheKey, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        var itemQuery = _context.ToeicPracticeSetItems
            .AsNoTracking()
            .Where(item => item.PracticeSetId == practiceSet.Id);

        if (!canManageContent)
        {
            itemQuery = itemQuery.Where(item => item.Question.Status == ContentStatus.Active);
        }

        IReadOnlyList<ContentQuestionDto> questions = await itemQuery
            .OrderBy(item => item.OrderIndex)
            .Select(item => item.Question)
            .Select(ContentProjections.Question)
            .ToListAsync(cancellationToken);

        IReadOnlyList<ContentPassageDto> passages = await _context.LoadPassagesAsync(questions, cancellationToken);

        // Learners practise through attempts, where answers are revealed per checked question.
        // The preview must not hand them the answer key, explanations, scripts or translations up front.
        if (!canManageContent)
        {
            questions = questions.Select(question => question.WithoutAnswers()).ToList();
            passages = passages.Select(passage => passage.WithoutAnswers()).ToList();
        }

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
            IncludesAnswers: canManageContent,
            ContentPartsBuilder.Build(passages, questions));

        await _cache.SetAsync(cacheKey, result, cancellationToken);

        return result;
    }
}
