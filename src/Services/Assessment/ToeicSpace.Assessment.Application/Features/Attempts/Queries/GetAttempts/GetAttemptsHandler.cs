using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Services.Attempts;
using ToeicSpace.BuildingBlocks.Pagination;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempts;

public sealed class GetAttemptsHandler : IRequestHandler<GetAttemptsQuery, PaginatedResult<AttemptSummaryDto>>
{
    private readonly AttemptAccess _access;
    private readonly AttemptSummaryReader _summaries;

    public GetAttemptsHandler(AttemptAccess access, AttemptSummaryReader summaries)
    {
        _access = access;
        _summaries = summaries;
    }

    public async Task<PaginatedResult<AttemptSummaryDto>> Handle(GetAttemptsQuery request, CancellationToken cancellationToken)
    {
        var owner = _access.RequireLearner();
        var query = _access.Eligible().AsNoTracking().Where(attempt => attempt.UserId == owner);

        if (request.Mode is { } mode)
        {
            query = query.Where(attempt => attempt.Mode == mode);
        }

        if (request.Kind == AttemptSourceKind.Test)
        {
            query = query.Where(attempt => attempt.TestId != null);
        }
        else if (request.Kind == AttemptSourceKind.PracticeSet)
        {
            query = query.Where(attempt => attempt.PracticeSetId != null);
        }

        if (request.SourceId is { } sourceId)
        {
            query = query.Where(attempt => attempt.TestId == sourceId || attempt.PracticeSetId == sourceId);
        }

        if (request.Status is { } status)
        {
            query = query.Where(attempt => attempt.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var attempts = await query
            .OrderByDescending(attempt => attempt.StartTime)
            .ThenByDescending(attempt => attempt.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<AttemptSummaryDto>(
            await _summaries.SummarizeAsync(attempts, cancellationToken),
            total,
            request.Page,
            request.PageSize);
    }
}
