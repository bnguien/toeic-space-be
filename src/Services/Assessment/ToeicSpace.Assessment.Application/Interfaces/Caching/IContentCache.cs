namespace ToeicSpace.Assessment.Application.Interfaces.Caching;

/// <summary>
/// Cache for read-heavy test and practice content.
/// </summary>
public interface IContentCache
{
    Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken)
        where T : class;

    Task SetAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken)
        where T : class;

    Task RemoveAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken);
}
