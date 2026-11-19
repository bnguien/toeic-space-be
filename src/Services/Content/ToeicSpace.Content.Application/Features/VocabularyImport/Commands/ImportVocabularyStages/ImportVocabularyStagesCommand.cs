using ToeicSpace.Content.Application.DTOs.VocabularyImport;

namespace ToeicSpace.Content.Application.Features.VocabularyImport.Commands.ImportVocabularyStages;

/// <summary>Idempotent upsert of one or more stage files in a single transaction.</summary>
public sealed record ImportVocabularyStagesCommand(IReadOnlyList<VocabularyImportFile> Files)
    : IRequest<VocabularyImportResultDto>
{
    public const int MaxFiles = 10;
    public const long MaxFileBytes = 5 * 1024 * 1024;
}
