using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.BuildingBlocks.Pagination;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempts;

/// <summary>The learner's attempts, newest first.</summary>
/// <param name="SourceId">A test or practice set ID.</param>
public sealed record GetAttemptsQuery(
    AttemptMode? Mode = null,
    AttemptSourceKind? Kind = null,
    Guid? SourceId = null,
    AttemptStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedResult<AttemptSummaryDto>>;
