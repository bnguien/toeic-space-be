using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartPracticeSetAttempt;

/// <summary>
/// Starts a practice session on a published practice set, or returns the in-progress one.
/// </summary>
/// <param name="ExpectedGeneration">The lifecycle fence from GET practice-sets/{id}/attempts/active.</param>
/// <param name="Count">Soft target for Continue/Random/Mistakes (5–200); passage groups are kept whole.</param>
/// <param name="Restart">Abandon the in-progress session first instead of returning it.</param>
public sealed record StartPracticeSetAttemptCommand(
    Guid PracticeSetId,
    long ExpectedGeneration,
    PracticeSelection Selection = PracticeSelection.All,
    int? Count = null,
    bool Restart = false) : IRequest<AttemptDto>;
