namespace ToeicSpace.BuildingBlocks.Storage.Abstractions;

public interface IObjectStorageService
{
    Task<string> GenerateUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);
}