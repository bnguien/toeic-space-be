namespace ToeicSpace.Assessment.Application.Practice.Common;

public interface IPracticeSetContent
{
    string Code { get; }

    string Title { get; }

    string? Description { get; }

    PracticeSetKind Kind { get; }

    ToeicPart Part { get; }

    int? Level { get; }

    int? TargetScore { get; }

    int DurationMinutes { get; }

    int OrderIndex { get; }
}
