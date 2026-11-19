using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.DTOs.Tests;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Features.Tests.Commands.UpdateTest;

public sealed record UpdateTestCommand(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    string? Category,
    int? Year,
    int DurationMinutes = ToeicPartRules.StandardTestDurationMinutes,
    string? AudioUrl = null,
    bool IsActive = true) : IRequest<TestDetailDto>, ITestContent;
