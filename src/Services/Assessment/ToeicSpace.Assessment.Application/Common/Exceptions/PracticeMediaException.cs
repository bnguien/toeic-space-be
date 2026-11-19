namespace ToeicSpace.Assessment.Application.Common.Exceptions;

/// <summary>
/// Media of an attempt could not be preserved. <see cref="Reason"/> is a bounded diagnostic code,
/// never an upstream URL, response body or storage credential.
/// </summary>
public sealed class PracticeMediaException(string reason, string? sourceReference = null)
    : AppException(ErrorType.Conflict, "Required media could not be preserved.", ErrorCodes.MediaPreservationFailed)
{
    public string Reason { get; } = reason;

    public string? SourceReference { get; } = sourceReference;
}
