using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetActivePracticeSetAttempt;

public sealed class GetActivePracticeSetAttemptHandler
    : IRequestHandler<GetActivePracticeSetAttemptQuery, ActivePracticeAttemptDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;

    public GetActivePracticeSetAttemptHandler(IAssessmentDbContext context, AttemptAccess access)
    {
        _context = context;
        _access = access;
    }

    public async Task<ActivePracticeAttemptDto> Handle(
        GetActivePracticeSetAttemptQuery request,
        CancellationToken cancellationToken)
    {
        var owner = _access.RequireLearner();
        var generation = await _context.PracticeAttemptStates
            .AsNoTracking()
            .Where(state => state.UserId == owner && state.PracticeSetId == request.PracticeSetId)
            .Select(state => (long?)state.Generation)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;
        var cutoff = _access.Now.AddDays(-AttemptAccess.RetentionDays);
        var attemptId = await _access.InProgressFor(owner, request.PracticeSetId)
            .AsNoTracking()
            .Where(attempt => attempt.PracticeSetId == request.PracticeSetId && attempt.LastActivityAt > cutoff)
            .Select(attempt => (Guid?)attempt.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return new ActivePracticeAttemptDto(generation, attemptId);
    }
}
