using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Application.Services.Enrichment;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.AiFillWord;

public sealed class AiFillWordHandler(ILearnerLibraryRepository library, WordEnricher enricher, LibraryWordService words,
    EnrichmentSettings settings, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<AiFillWordHandler> logger)
    : IRequestHandler<AiFillWordCommand, AiFillResultDto>
{
    public async Task<AiFillResultDto> Handle(AiFillWordCommand request, CancellationToken ct)
    {
        var saved = await library.GetSavedWordForUpdateAsync(request.LearnerId, request.SavedWordId, ct)
            ?? throw VocabularyErrors.SavedWordNotFound();
        var word = saved.Word!;
        if (!word.IsCustom) throw VocabularyErrors.WordNotCustom();

        var known = new WordGloss
        {
            MeaningVi = word.MeaningVi, Phonetic = word.Phonetic, PartOfSpeech = word.PartOfSpeech,
            DefinitionEn = word.DefinitionEn, Example = word.Example, ExampleVi = word.ExampleVi
        };
        var result = await enricher.EnrichAsync(new EnrichRequest(word.Headword, null, word.PartOfSpeech, known),
            new EnrichOptions(AllowAi: true, Interactive: true, TimeSpan.FromSeconds(settings.AiFillDeadlineSeconds),
                FillAllBlanks: true), ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var filled = CustomWordFiller.FillBlanks(word, result.Gloss, now);
        if (filled.Count > 0)
        {
            saved.UpdatedAt = now;
            await unitOfWork.SaveChangesAsync(ct);
        }
        logger.LogInformation("AI fill of custom word {WordId} filled {Count} fields (AI used: {AiUsed})",
            word.Id, filled.Count, result.AiUsed);
        return AiFillResultDto.From(await words.ToDtoAsync(saved, request.TzOffset, ct), filled);
    }
}
