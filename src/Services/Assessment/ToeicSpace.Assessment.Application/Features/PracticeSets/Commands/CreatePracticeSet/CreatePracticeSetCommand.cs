using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.DTOs.PracticeSets;


namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.CreatePracticeSet;

public sealed record CreatePracticeSetCommand(
    string Code,
    string Title,
    string? Description,
    PracticeSetKind Kind,
    ToeicPart Part,
    int? Level,
    int? TargetScore,
    int DurationMinutes,
    int OrderIndex = 0,
    IReadOnlyList<Guid>? QuestionIds = null,
    ContentStatus Status = ContentStatus.Draft) : IRequest<PracticeSetDetailDto>, IPracticeSetContent;
