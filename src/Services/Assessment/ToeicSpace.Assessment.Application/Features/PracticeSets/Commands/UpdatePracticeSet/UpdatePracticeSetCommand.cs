using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.DTOs.PracticeSets;


namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.UpdatePracticeSet;

public sealed record UpdatePracticeSetCommand(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    PracticeSetKind Kind,
    ToeicPart Part,
    int? Level,
    int? TargetScore,
    int DurationMinutes,
    int OrderIndex = 0,
    DateTime? ExpectedUpdatedAt = null) : IRequest<PracticeSetDetailDto>, IPracticeSetContent;
