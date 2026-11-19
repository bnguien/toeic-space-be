using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeSetsProgress;

/// <summary>Progress of the learner on every published practice set (optionally of one part).</summary>
public sealed record GetPracticeSetsProgressQuery(int? Part = null) : IRequest<IReadOnlyList<PracticeSetProgressDto>>;
