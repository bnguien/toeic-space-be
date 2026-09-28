using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Common.Content;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Interfaces.Caching;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Tests.Common;

namespace ToeicSpace.Assessment.Application.Tests.Queries.GetFullTest;

public sealed class GetFullTestHandler : IRequestHandler<GetFullTestQuery, FullTestDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IContentCache _cache;

    public GetFullTestHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser,
        IContentCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<FullTestDto> Handle(
        GetFullTestQuery request,
        CancellationToken cancellationToken)
    {
        var canManageContent = _currentUser.CanManageContent;

        if (request.IncludeAnswers && !canManageContent)
        {
            throw AppException.Forbidden(
                "Only content managers can view the answer key of a test.",
                ErrorCodes.AnswerKeyForbidden);
        }

        var cacheKey = ContentCacheKeys.FullTest(request.Id, canManageContent, request.IncludeAnswers);
        var cached = await _cache.GetAsync<FullTestDto>(cacheKey, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        var test = await _context.ToeicTests
            .AsNoTracking()
            .VisibleTo(canManageContent)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicTest), request.Id);

        var questionQuery = _context.ToeicQuestions
            .AsNoTracking()
            .Where(question => question.TestId == test.Id);

        if (!canManageContent)
        {
            questionQuery = questionQuery.Where(question => question.Status == ContentStatus.Active);
        }

        IReadOnlyList<ContentQuestionDto> questions = await questionQuery
            .OrderBy(question => question.QuestionNumber)
            .ThenBy(question => question.OrderIndex)
            .ThenBy(question => question.Id)
            .Select(ContentProjections.Question)
            .ToListAsync(cancellationToken);

        IReadOnlyList<ContentPassageDto> passages = await _context.LoadPassagesAsync(questions, cancellationToken);

        if (!request.IncludeAnswers)
        {
            questions = questions.Select(question => question.WithoutAnswers()).ToList();
            passages = passages.Select(passage => passage.WithoutAnswers()).ToList();
        }

        var result = new FullTestDto(
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
            request.IncludeAnswers,
            ContentPartsBuilder.Build(passages, questions));

        await _cache.SetAsync(cacheKey, result, cancellationToken);

        return result;
    }
}
