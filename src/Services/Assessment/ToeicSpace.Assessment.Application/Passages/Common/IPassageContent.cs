namespace ToeicSpace.Assessment.Application.Passages.Common;

public interface IPassageContent
{
    Guid? TestId { get; }

    ToeicPart Part { get; }

    string? PassageType { get; }

    string? Title { get; }

    string? Content { get; }

    string? AudioUrl { get; }

    string? ImageUrl { get; }

    string? Transcript { get; }

    int OrderIndex { get; }
}
