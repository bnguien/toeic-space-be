using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarImport.Commands.ImportGrammarContent;

/// <summary>
/// Idempotent upsert of the grammar bundle(s) built by scripts/grammar_data_pipeline.py (data/grammar/*.json.gz):
/// one document per (kind, key). Documents missing from the files are kept.
/// </summary>
public sealed record ImportGrammarContentCommand(IReadOnlyList<GrammarImportFile> Files) : IRequest<GrammarImportResultDto>
{
    public const int MaxFiles = 5;
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const long MaxUncompressedBytes = 60 * 1024 * 1024;
}
