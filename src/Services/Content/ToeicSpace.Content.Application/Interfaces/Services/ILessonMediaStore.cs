namespace ToeicSpace.Content.Application.Interfaces.Services;

public interface ILessonMediaStore
{
    Task<string> PresignUploadAsync(string key, string contentType, TimeSpan lifetime, CancellationToken ct);
    Task<StoredThumbnail?> HeadAsync(string key, CancellationToken ct);
    Task<string> PresignReadAsync(string key, TimeSpan lifetime, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
