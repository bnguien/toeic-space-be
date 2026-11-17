using ToeicSpace.Assessment.Domain.Exceptions;

namespace ToeicSpace.Assessment.Application.Attempts;

// A bounded diagnostic code, never an upstream URL, response body or storage credential.
public sealed class PracticeMediaException(string reason, string? sourceReference = null)
    : AppException(ErrorType.Conflict, "Required media could not be preserved.", "media_preservation_failed")
{
    public string Reason { get; } = reason;
    public string? SourceReference { get; } = sourceReference;
}
