using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSetStatus;

public sealed record UpdatePracticeSetStatusCommand(
    Guid Id,
    ContentStatus Status) : IRequest<PracticeSetDetailDto>;
