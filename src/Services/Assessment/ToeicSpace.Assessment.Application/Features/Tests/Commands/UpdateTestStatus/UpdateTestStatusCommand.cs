using ToeicSpace.Assessment.Application.DTOs.Tests;


namespace ToeicSpace.Assessment.Application.Features.Tests.Commands.UpdateTestStatus;

public sealed record UpdateTestStatusCommand(
    Guid Id,
    ContentStatus Status) : IRequest<TestDetailDto>;
