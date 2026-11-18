using ToeicSpace.Assessment.Application.DTOs.PracticeSets;


namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.UpdatePracticeSetStatus;

public sealed record UpdatePracticeSetStatusCommand(
    Guid Id,
    ContentStatus Status) : IRequest<PracticeSetDetailDto>;
