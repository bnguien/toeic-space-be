using Microsoft.Extensions.Hosting;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Models.News;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Infrastructure.Services.News;

/// <summary>
/// Cover images on the local file system (<c>News:CoverDirectory</c>, relative to the content root). Names are
/// content hashes, so a stored file never changes and can be cached forever by browsers.
/// </summary>
public sealed class FileSystemNewsCoverStore(NewsSettings settings, IHostEnvironment environment) : INewsCoverStore
{
    private readonly string _root = Path.GetFullPath(settings.CoverDirectory, environment.ContentRootPath);

    public bool Exists(string fileName) => GetPhysicalPath(fileName) is not null;

    public async Task<bool> SaveAsync(string fileName, byte[] content, CancellationToken ct)
    {
        var path = Resolve(fileName);
        if (File.Exists(path) && new FileInfo(path).Length == content.LongLength
            && (await File.ReadAllBytesAsync(path, ct)).AsSpan().SequenceEqual(content))
            return false;
        Directory.CreateDirectory(_root);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllBytesAsync(temporary, content, ct);
        File.Move(temporary, path, overwrite: true);
        return true;
    }

    public string? GetPhysicalPath(string fileName)
    {
        if (!NewsImportParser.IsCoverName(fileName)) return null;
        var path = Resolve(fileName);
        return File.Exists(path) ? path : null;
    }

    private string Resolve(string fileName)
    {
        if (!NewsImportParser.IsCoverName(fileName))
            throw new ArgumentException("Invalid cover file name.", nameof(fileName));
        return Path.Combine(_root, fileName);
    }
}
