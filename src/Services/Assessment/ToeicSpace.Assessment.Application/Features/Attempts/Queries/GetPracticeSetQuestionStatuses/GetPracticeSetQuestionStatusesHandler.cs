using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeSetQuestionStatuses;

public sealed class GetPracticeSetQuestionStatusesHandler
    : IRequestHandler<GetPracticeSetQuestionStatusesQuery, IReadOnlyList<PracticeQuestionStatusDto>>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AnswerHistoryReader _history;

    public GetPracticeSetQuestionStatusesHandler(IAssessmentDbContext context, AttemptAccess access, AnswerHistoryReader history)
    {
        _context = context;
        _access = access;
        _history = history;
    }

    public async Task<IReadOnlyList<PracticeQuestionStatusDto>> Handle(
        GetPracticeSetQuestionStatusesQuery request,
        CancellationToken cancellationToken)
    {
        var owner = _access.RequireLearner();
        var visible = await _context.ToeicPracticeSets
            .AsNoTracking()
            .AnyAsync(set => set.Id == request.PracticeSetId && set.Status == ContentStatus.Active, cancellationToken);

        if (!visible)
        {
            throw AppException.NotFound(nameof(ToeicPracticeSet), request.PracticeSetId);
        }

        var items = await _context.ToeicPracticeSetItems
            .AsNoTracking()
            .Where(item => item.PracticeSetId == request.PracticeSetId)
            .OrderBy(item => item.OrderIndex)
            .Join(_context.ToeicQuestions.IgnoreQueryFilters(),
                item => item.QuestionId,
                question => question.Id,
                (item, question) => new { item.OrderIndex, question.Id, question.QuestionNumber, question.Part })
            .ToListAsync(cancellationToken);
        var latest = await _history.LatestGradedAsync(owner, items.Select(item => item.Id).ToList(), cancellationToken);

        return items
            .OrderBy(item => item.OrderIndex)
            .Select(item =>
            {
                var graded = latest.GetValueOrDefault(item.Id);
                var status = graded is null
                    ? QuestionProgressStatus.Unanswered
                    : graded.IsCorrect ? QuestionProgressStatus.Correct : QuestionProgressStatus.Wrong;

                return new PracticeQuestionStatusDto(item.Id, item.OrderIndex, item.QuestionNumber, (int)item.Part,
                    status, graded?.GradedAt);
            })
            .ToList();
    }
}
