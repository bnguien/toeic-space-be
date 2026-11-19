using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.SaveAnswer;

/// <summary>
/// Autosaves one answer with optimistic concurrency (<paramref name="ExpectedRevision"/>) and idempotent retries
/// (<paramref name="MutationId"/>). <paramref name="TimeSpentSeconds"/> is a delta added to the question's time.
/// </summary>
public sealed record SaveAnswerCommand(
    Guid AttemptId,
    Guid QuestionId,
    AnswerOption? Answer,
    long ExpectedRevision,
    Guid MutationId,
    int? TimeSpentSeconds = null) : IRequest<SavedAnswerDto>;
