using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartTestAttempt;

/// <summary>
/// Starts (or resumes) the learner's attempt on a published test. One in-progress attempt per test:
/// an existing one is returned unless <paramref name="Restart"/> abandons it first.
/// </summary>
/// <param name="Parts">Required for Practice (distinct, 1–7); FullTest always covers Parts 1–7.</param>
public sealed record StartTestAttemptCommand(
    Guid TestId,
    AttemptMode? Mode,
    IReadOnlyList<int>? Parts = null,
    bool Restart = false) : IRequest<AttemptDto>;
