using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetTestsProgress;

/// <summary>Progress per test the learner has touched (tests never started are omitted).</summary>
public sealed record GetTestsProgressQuery : IRequest<IReadOnlyList<TestProgressDto>>;
