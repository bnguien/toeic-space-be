namespace ToeicSpace.Assessment.Application.Common.Validation;

public interface ITestContent
{
    string Code { get; }

    string Title { get; }

    string? Description { get; }

    string? Category { get; }

    int? Year { get; }

    int DurationMinutes { get; }

    string? AudioUrl { get; }

    bool IsActive { get; }
}
