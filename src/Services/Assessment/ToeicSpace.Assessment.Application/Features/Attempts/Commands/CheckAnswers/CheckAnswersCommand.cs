using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.CheckAnswers;

/// <summary>
/// Practice only: grades and locks the given questions and reveals their answer key, explanation, transcript and
/// passages. Checking an already checked question is a no-op that reveals it again.
/// </summary>
public sealed record CheckAnswersCommand(
    Guid AttemptId,
    IReadOnlyList<Guid> QuestionIds) : IRequest<RevealDto>;
