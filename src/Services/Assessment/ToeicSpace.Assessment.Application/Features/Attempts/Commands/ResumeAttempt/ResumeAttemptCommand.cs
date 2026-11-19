using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.ResumeAttempt;

/// <summary>Marks learner activity on an in-progress attempt and returns it.</summary>
public sealed record ResumeAttemptCommand(Guid AttemptId) : IRequest<AttemptDto>;
