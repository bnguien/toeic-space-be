using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdatePersonalWord;

/// <summary>
/// Progress is keyed by word id, so an edit never moves or loses progress; the response is 201 when the edit added the
/// word to the library.
/// </summary>
public sealed class UpdatePersonalWordHandler(ILearnerLibraryRepository library, IVocabularyCatalogRepository catalog,
    LibraryWordService words, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<UpdatePersonalWordHandler> logger)
    : IRequestHandler<UpdatePersonalWordCommand, SavedWordUpsertResult>
{
    public async Task<SavedWordUpsertResult> Handle(UpdatePersonalWordCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var saved = await library.GetSavedWordByWordForUpdateAsync(request.LearnerId, request.WordId, ct);
        // Official words and the learner's own custom words; another learner's private word is "not found".
        var word = saved?.Word
            ?? (await catalog.GetWordsAsync([request.WordId], request.LearnerId, ct)).FirstOrDefault()
            ?? throw VocabularyErrors.WordNotFound();
        var created = saved is null;

        if (word.IsCustom)
        {
            // A custom word lives and dies with its library row, so the row is always there.
            if (saved is null) throw VocabularyErrors.WordNotFound();
            await EditCustomAsync(saved.Word!, request, now, ct);
        }
        else
        {
            if (saved is null)
            {
                saved = new SavedWord { LearnerId = request.LearnerId, WordId = word.Id, SavedAt = now };
                library.AddSavedWord(saved);
            }
            EditOfficial(saved, word, request, now);
        }
        if (request.Note.IsSet) saved.Note = LibraryWordService.Clean(request.Note.Value);
        saved.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation(
            "Learner {LearnerId} edited word {WordId} (custom: {IsCustom}, added to library: {Created}, reset: {Reset})",
            request.LearnerId, word.Id, word.IsCustom, created, request.Reset);

        var stored = created ? await library.GetSavedWordForUpdateAsync(request.LearnerId, saved.Id, ct) : saved;
        return new SavedWordUpsertResult(await words.ToDtoAsync(stored!, request.TzOffset, ct), created);
    }

    /// <summary>Overrides on the learner's row; a blank value or the shared value clears the override.</summary>
    private static void EditOfficial(SavedWord saved, VocabularyWord shared, UpdatePersonalWordCommand request,
        DateTime now)
    {
        if (request.Reset) saved.ResetPersonal();
        if (request.Phonetic.IsSet) saved.MyPhonetic = SavedWord.PersonalValue(request.Phonetic.Value, shared.Phonetic);
        if (request.PartOfSpeech.IsSet)
            saved.MyPartOfSpeech = UpdatePersonalWordCommand.ParsePartOfSpeech(request.PartOfSpeech.Value) is { } pos
                && pos != shared.PartOfSpeech ? pos : null;
        if (request.MeaningVi.IsSet) saved.MyMeaningVi = SavedWord.PersonalValue(request.MeaningVi.Value, shared.MeaningVi);
        if (request.Example.IsSet) saved.MyExample = SavedWord.PersonalValue(request.Example.Value, shared.Example);
        if (request.ExampleVi.IsSet) saved.MyExampleVi = SavedWord.PersonalValue(request.ExampleVi.Value, shared.ExampleVi);
        saved.StampPersonalized(now);
    }

    /// <summary>The custom word is the learner's own: sent fields replace its values (the meaning cannot be blank).</summary>
    private async Task EditCustomAsync(VocabularyWord word, UpdatePersonalWordCommand request, DateTime now,
        CancellationToken ct)
    {
        if (request.MeaningVi.IsSet && string.IsNullOrWhiteSpace(request.MeaningVi.Value))
            throw VocabularyErrors.CustomMeaningRequired();
        var input = new CustomWordInput(word.Headword, Pick(request.Phonetic, word.Phonetic),
            UpdatePersonalWordCommand.ParsePartOfSpeech(request.PartOfSpeech.Value) ?? word.PartOfSpeech,
            Pick(request.MeaningVi, word.MeaningVi)!, word.DefinitionEn, Pick(request.Example, word.Example),
            Pick(request.ExampleVi, word.ExampleVi));
        await words.ApplyCustomAsync(word, request.LearnerId, input, now, ct);
    }

    private static string? Pick(Optional<string?> sent, string? current) => sent.IsSet ? sent.Value : current;
}
