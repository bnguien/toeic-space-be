namespace ToeicSpace.Content.Application.Interfaces.Services;

public sealed record StoredThumbnail(string ContentType, long SizeBytes, bool SignatureValid);

public interface IThumbnailObjectStore
{
    Task<string> PresignAsync(string key, string contentType, TimeSpan expiration, CancellationToken ct);
    Task<StoredThumbnail?> HeadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
