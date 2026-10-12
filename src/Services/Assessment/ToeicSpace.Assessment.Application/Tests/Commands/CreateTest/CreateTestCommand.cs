using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Tests.Common;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Tests.Commands.CreateTest;

public sealed record CreateTestCommand(
    string Code,
    string Title,
    string? Description,
    string? Category,
    int? Year,
    int DurationMinutes = ToeicPartRules.StandardTestDurationMinutes,
    string? AudioUrl = null,
    bool IsActive = true) : IRequest<TestDetailDto>, ITestContent;
