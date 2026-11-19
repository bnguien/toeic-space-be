using ToeicSpace.Content.Application.DTOs.LibraryImport;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services.Import;

namespace ToeicSpace.Content.Application.Features.LibraryImport.Commands.ParseImport;

public sealed class ParseImportHandler(ILearnerLibraryRepository library)
    : IRequestHandler<ParseImportCommand, ImportPreviewDto>
{
    public async Task<ImportPreviewDto> Handle(ParseImportCommand request, CancellationToken ct)
    {
        var parsed = string.IsNullOrWhiteSpace(request.Text)
            ? VocabularyTextParser.ParseJson(request.Json!)
            : VocabularyTextParser.ParseText(request.Text);
        var saved = (await library.GetSavedWordsAsync(request.LearnerId, ct))
            .Select(x => x.Word!.NormalizedHeadword).ToHashSet();
        return new ImportPreviewDto(
            parsed.Items.Select(x => new ImportPreviewItemDto(x.Headword, x.MeaningVi, x.Phonetic, x.PartOfSpeech,
                x.Example, x.ExampleVi, x.Category, x.Line, x.Duplicate,
                saved.Contains(ToeicSpace.Content.Domain.Entities.VocabularyWord.Normalize(x.Headword)))).ToArray(),
            parsed.Rejected.Select(x => new ImportRejectedLineDto(x.Line, x.Text, x.Code)).ToArray());
    }
}
