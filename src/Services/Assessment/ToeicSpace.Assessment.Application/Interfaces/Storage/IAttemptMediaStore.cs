namespace ToeicSpace.Assessment.Application.Interfaces.Storage;

/// <summary>
/// Retains immutable, content-addressed copies of attempt media so a snapshot keeps working after the source changes.
/// </summary>
public interface IAttemptMediaStore
{
    /// <summary>
    /// Keys are original absolute URLs; values are immutable URLs served by our gateway.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> PreserveAsync(IEnumerable<string> urls, CancellationToken cancellationToken);

    Task<(Stream Content, string ContentType)> OpenAsync(string hash, CancellationToken cancellationToken);
}
