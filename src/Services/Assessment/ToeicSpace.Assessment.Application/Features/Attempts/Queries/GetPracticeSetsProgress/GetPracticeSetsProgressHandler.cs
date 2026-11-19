using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeSetsProgress;

public sealed class GetPracticeSetsProgressHandler
    : IRequestHandler<GetPracticeSetsProgressQuery, IReadOnlyList<PracticeSetProgressDto>>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AnswerHistoryReader _history;

    public GetPracticeSetsProgressHandler(IAssessmentDbContext context, AttemptAccess access, AnswerHistoryReader history)
    {
        _context = context;
        _access = access;
        _history = history;
    }

    public async Task<IReadOnlyList<PracticeSetProgressDto>> Handle(
        GetPracticeSetsProgressQuery request,
        CancellationToken cancellationToken)
    {
        var owner = _access.RequireLearner();
        var sets = _context.ToeicPracticeSets.AsNoTracking().Where(set => set.Status == ContentStatus.Active);

        if (request.Part is { } part)
        {
            var toeicPart = (ToeicPart)part;
            sets = sets.Where(set => set.Part == toeicPart);
        }

        var setIds = await sets
            .OrderBy(set => set.OrderIndex)
            .ThenBy(set => set.Code)
            .Select(set => set.Id)
            .ToListAsync(cancellationToken);
        var items = await _context.ToeicPracticeSetItems
            .AsNoTracking()
            .Where(item => setIds.Contains(item.PracticeSetId))
            .Select(item => new { item.PracticeSetId, item.QuestionId })
            .ToListAsync(cancellationToken);
        var latest = await _history.LatestGradedAsync(
            owner, items.Select(item => item.QuestionId).Distinct().ToList(), cancellationToken);

        var cutoff = _access.Now.AddDays(-AttemptAccess.RetentionDays);
        var active = await _access.Eligible()
            .AsNoTracking()
            .Where(attempt => attempt.UserId == owner
                && attempt.Status == AttemptStatus.InProgress
                && attempt.PracticeSetId != null
                && setIds.Contains(attempt.PracticeSetId.Value)
                && attempt.LastActivityAt > cutoff)
            .Select(attempt => new { attempt.Id, SetId = attempt.PracticeSetId!.Value })
            .ToListAsync(cancellationToken);
        var activeBySet = active.GroupBy(item => item.SetId).ToDictionary(group => group.Key, group => group.First().Id);
        var questionsBySet = items.GroupBy(item => item.PracticeSetId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.QuestionId).Distinct().ToList());

        return setIds
            .Select(setId =>
            {
                var questions = questionsBySet.GetValueOrDefault(setId) ?? [];
                var graded = questions.Where(latest.ContainsKey).Select(id => latest[id]).ToList();

                return new PracticeSetProgressDto(
                    setId,
                    questions.Count,
                    graded.Count,
                    graded.Count(answer => answer.IsCorrect),
                    graded.Count(answer => !answer.IsCorrect),
                    activeBySet.TryGetValue(setId, out var attemptId) ? attemptId : null);
            })
            .ToList();
    }
}
