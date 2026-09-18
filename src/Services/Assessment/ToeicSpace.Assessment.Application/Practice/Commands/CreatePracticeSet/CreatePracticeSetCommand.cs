using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Commands.CreatePracticeSet;

public sealed record CreatePracticeSetCommand(
    string Code,
    string Title,
    string? Description,
    PracticeSetKind Kind,
    ToeicPart Part,
    int? Level,
    int? TargetScore,
    int DurationMinutes,
    int OrderIndex = 0) : IRequest<PracticeSetDetailDto>, IPracticeSetContent;
