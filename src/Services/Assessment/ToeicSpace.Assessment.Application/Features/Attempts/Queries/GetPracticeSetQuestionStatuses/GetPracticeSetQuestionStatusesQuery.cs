using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeSetQuestionStatuses;

/// <summary>The learner's status on each question of a published practice set, in set order.</summary>
public sealed record GetPracticeSetQuestionStatusesQuery(Guid PracticeSetId)
    : IRequest<IReadOnlyList<PracticeQuestionStatusDto>>;
