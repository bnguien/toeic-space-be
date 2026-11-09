using ToeicSpace.Content.Application.DTOs.DictationImport;

namespace ToeicSpace.Content.Application.Features.DictationImport.Commands.ImportDictationPacks;

/// <summary>Idempotent upsert of pack files from <c>data/dictation</c>; one transaction per file.</summary>
public sealed record ImportDictationPacksCommand(IReadOnlyList<DictationImportFile> Files)
    : IRequest<DictationImportResultDto>
{
    public const int MaxFiles = 30;
    /// <summary>Upload size of one file (gzip or plain JSON).</summary>
    public const long MaxFileBytes = 10 * 1024 * 1024;
    /// <summary>Size of one file once decompressed.</summary>
    public const long MaxContentBytes = 24 * 1024 * 1024;
}
