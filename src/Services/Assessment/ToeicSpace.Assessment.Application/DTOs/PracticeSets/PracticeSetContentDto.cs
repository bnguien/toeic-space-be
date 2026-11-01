using ToeicSpace.Assessment.Application.DTOs.Content;

namespace ToeicSpace.Assessment.Application.DTOs.PracticeSets;

public sealed record PracticeSetContentDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    PracticeSetKind Kind,
    ToeicPart Part,
    int? Level,
    int? TargetScore,
    int DurationMinutes,
    int QuestionCount,
    bool IncludesAnswers,
    IReadOnlyList<ContentPartDto> Parts);
