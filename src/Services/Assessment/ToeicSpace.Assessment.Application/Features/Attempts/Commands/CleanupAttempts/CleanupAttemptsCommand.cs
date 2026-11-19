using ToeicSpace.Assessment.Application.Models.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.CleanupAttempts;

/// <summary>
/// Background maintenance (one batch): auto-submits FullTest attempts whose deadline passed and deletes practice
/// attempts inactive for longer than the retention period.
/// </summary>
public sealed record CleanupAttemptsCommand : IRequest<AttemptCleanupResult>;
