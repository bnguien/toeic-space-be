using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.SubmitAttempt;

/// <summary>
/// Grades and completes an attempt. Idempotent: submitting a completed attempt returns the same result.
/// A FullTest submitted after its deadline is still accepted (answers saved in time are graded).
/// </summary>
public sealed record SubmitAttemptCommand(Guid AttemptId) : IRequest<AttemptResultDto>;
