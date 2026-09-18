using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Tests.Commands.UpdateTestStatus;

public sealed record UpdateTestStatusCommand(
    Guid Id,
    ContentStatus Status) : IRequest<TestDetailDto>;
