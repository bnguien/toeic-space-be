namespace ToeicSpace.Content.Application.Interfaces.Services;

/// <summary>Content-addressed cover images (<c>&lt;hash&gt;.webp</c>) served as public static files.</summary>
public interface INewsCoverStore
{
    bool Exists(string fileName);
    /// <summary>Stores the file; false when an identical file is already stored.</summary>
    Task<bool> SaveAsync(string fileName, byte[] content, CancellationToken ct);
    /// <summary>Absolute path of a stored cover, or null.</summary>
    string? GetPhysicalPath(string fileName);
}
