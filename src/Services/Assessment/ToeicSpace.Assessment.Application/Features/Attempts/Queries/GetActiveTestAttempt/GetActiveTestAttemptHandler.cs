using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetActiveTestAttempt;

public sealed class GetActiveTestAttemptHandler : IRequestHandler<GetActiveTestAttemptQuery, ActiveTestAttemptDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;

    public GetActiveTestAttemptHandler(IAssessmentDbContext context, AttemptAccess access)
    {
        _context = context;
        _access = access;
    }

    public async Task<ActiveTestAttemptDto> Handle(GetActiveTestAttemptQuery request, CancellationToken cancellationToken)
    {
        var owner = _access.RequireLearner();
        var now = AttemptAccess.Utc(_access.Now);
        var attempt = await _access.InProgressFor(owner, request.TestId)
            .AsNoTracking()
            .Where(item => item.TestId == request.TestId)
            .SingleOrDefaultAsync(cancellationToken);

        if (attempt is null || _access.IsExpired(attempt))
        {
            return new ActiveTestAttemptDto(null, null, [], 0, 0, null, now);
        }

        var answered = await _context.ToeicAttemptAnswers
            .CountAsync(answer => answer.AttemptId == attempt.Id && answer.UserAnswer != null, cancellationToken);

        return new ActiveTestAttemptDto(
            attempt.Id,
            attempt.Mode,
            AttemptParts.Parse(attempt.SelectedParts).Select(part => (int)part).ToList(),
            answered,
            attempt.TotalQuestions,
            attempt.Deadline is { } deadline ? AttemptAccess.Utc(deadline) : null,
            now);
    }
}
