using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.LibraryImport;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Application.Services.Enrichment;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.LibraryImport.Commands.ImportWords;

/// <summary>
/// Per-row results: a row that cannot be saved is reported in <c>failed</c> and never aborts the others.
/// Failure codes: no_latin_letter, too_many_words, too_long, meaning_missing.
/// </summary>
public sealed class ImportWordsHandler(ILearnerLibraryRepository library, ILearnerProgressRepository progress,
    ICuratedLexicon lexicon, WordEnricher enricher, LibraryWordService words, EnrichmentSettings settings,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<ImportWordsHandler> logger)
    : IRequestHandler<ImportWordsCommand, ImportResultDto>
{
    public async Task<ImportResultDto> Handle(ImportWordsCommand request, CancellationToken ct)
    {
        await words.EnsureFolderAsync(request.LearnerId, request.FolderId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var failed = new List<ImportFailureDto>();
        var rows = new List<Row>();
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var input = InputNormalizer.Normalize(item.Headword);
            var code = !InputNormalizer.HasLatinLetter(input.Text) ? "no_latin_letter"
                : input.TokenCount > InputNormalizer.MaxPhraseTokens ? "too_many_words"
                : input.Text.Length > VocabularyWord.CustomHeadwordMaxLength ? "too_long"
                : null;
            if (code is not null) failed.Add(new ImportFailureDto(i, item.Headword, code));
            else rows.Add(new Row(i, item, input));
        }

        // Official words: same normalized headword and, when given, the same part of speech.
        foreach (var row in rows)
            if (await lexicon.FindAsync(row.Input.Key, row.Item.PartOfSpeech, ct) is { } match
                && (row.Item.PartOfSpeech is null || match.Gloss.PartOfSpeech == row.Item.PartOfSpeech))
                row.OfficialWordId = match.WordId;

        var custom = await library.GetCustomWordsForUpdateAsync(request.LearnerId,
            rows.Where(x => x.OfficialWordId is null).Select(x => x.Input.Key).Distinct().ToArray(), ct);
        foreach (var row in rows.Where(x => x.OfficialWordId is null))
            row.Existing = custom.FirstOrDefault(w => w.NormalizedHeadword == row.Input.Key
                && (row.Item.PartOfSpeech is null || w.PartOfSpeech == row.Item.PartOfSpeech));

        // Missing fields of custom words go through the enrichment pipeline in one batch.
        var customRows = rows.Where(x => x.OfficialWordId is null).ToArray();
        var enriched = await enricher.EnrichManyAsync(customRows.Select(x => new EnrichRequest(x.Input.Text, null,
                x.Item.PartOfSpeech ?? x.Existing?.PartOfSpeech, Known(x))).ToArray(),
            new EnrichOptions(AllowAi: true, Interactive: false, TimeSpan.FromSeconds(settings.ImportDeadlineSeconds)), ct);
        for (var i = 0; i < customRows.Length; i++) customRows[i].Gloss = enriched[i].Gloss;

        var folders = new FolderResolver(library, request.LearnerId, now);
        var created = 0;
        var updated = 0;
        var savedIds = new List<Guid>();
        var pendingSaved = new Dictionary<Guid, SavedWord>();
        var batchWords = new Dictionary<(string, PartOfSpeech), VocabularyWord>();
        foreach (var row in rows)
        {
            var folderId = request.CreateFoldersFromCategories && !string.IsNullOrWhiteSpace(row.Item.Category)
                ? await folders.GetOrCreateAsync(row.Item.Category, ct)
                : request.FolderId;
            Guid wordId;
            if (row.OfficialWordId is { } official) wordId = official;
            else
            {
                var gloss = row.Gloss!;
                var word = row.Existing;
                var pos = word?.PartOfSpeech ?? gloss.PartOfSpeech
                    ?? (row.Input.Kind == WordKind.Phrase ? PartOfSpeech.Phrase : PartOfSpeech.Other);
                if (word is null && batchWords.TryGetValue((row.Input.Key, pos), out var inBatch)) word = inBatch;
                if (word is null)
                {
                    if (string.IsNullOrWhiteSpace(gloss.MeaningVi))
                    {
                        failed.Add(new ImportFailureDto(row.Index, row.Item.Headword, "meaning_missing"));
                        continue;
                    }
                    word = new VocabularyWord
                    {
                        OwnerKey = request.LearnerId, Headword = row.Input.Text, NormalizedHeadword = row.Input.Key,
                        PartOfSpeech = pos, MeaningVi = CustomWordFiller.Cut(gloss.MeaningVi, VocabularyWord.CustomMeaningMaxLength)!,
                        CreatedAt = now
                    };
                    library.AddWord(word);
                    batchWords[(row.Input.Key, pos)] = word;
                }
                CustomWordFiller.FillBlanks(word, gloss, now);
                wordId = word.Id;
            }

            var saved = pendingSaved.GetValueOrDefault(wordId)
                ?? await library.GetSavedWordByWordForUpdateAsync(request.LearnerId, wordId, ct);
            if (saved is null)
            {
                saved = new SavedWord
                {
                    LearnerId = request.LearnerId, WordId = wordId, FolderId = folderId, SavedAt = now, UpdatedAt = now
                };
                library.AddSavedWord(saved);
                pendingSaved[wordId] = saved;
                created++;
            }
            else
            {
                // Already in the library (or earlier in this batch): only an unfiled entry is moved to the target.
                if (saved.FolderId is null && folderId is not null)
                {
                    saved.FolderId = folderId;
                    saved.UpdatedAt = now;
                }
                updated++;
            }
            if (!savedIds.Contains(wordId)) savedIds.Add(wordId);
        }
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Learner {LearnerId} imported words: {Created} created, {Updated} updated, {Failed} failed",
            request.LearnerId, created, updated, failed.Count);

        var today = SpacedRepetitionScheduler.LocalDay(now, request.TzOffset);
        var library2 = (await library.GetSavedWordsAsync(request.LearnerId, ct)).ToDictionary(x => x.WordId);
        var rowsProgress = (await progress.GetForWordsAsync(request.LearnerId, savedIds, ct)).ToDictionary(x => x.WordId);
        var items = savedIds.Where(library2.ContainsKey)
            .Select(id => library2[id].ToDto(rowsProgress.GetValueOrDefault(id), today)).ToArray();
        return new ImportResultDto(created, updated, failed.OrderBy(x => x.Index).ToArray(), items);
    }

    private static WordGloss Known(Row row)
    {
        var item = row.Item;
        var known = new WordGloss
        {
            MeaningVi = item.MeaningVi, Phonetic = item.Phonetic, PartOfSpeech = item.PartOfSpeech,
            Example = item.Example, ExampleVi = item.ExampleVi
        };
        if (row.Existing is { } word)
            known.Fill(new WordGloss
            {
                MeaningVi = word.MeaningVi, Phonetic = word.Phonetic, PartOfSpeech = word.PartOfSpeech,
                DefinitionEn = word.DefinitionEn, Example = word.Example, ExampleVi = word.ExampleVi
            }, GlossSource.Cache);
        return known;
    }

    private sealed class Row(int index, ImportItemDto item, NormalizedInput input)
    {
        public int Index { get; } = index;
        public ImportItemDto Item { get; } = item;
        public NormalizedInput Input { get; } = input;
        public Guid? OfficialWordId { get; set; }
        public VocabularyWord? Existing { get; set; }
        public WordGloss? Gloss { get; set; }
    }

    /// <summary>Finds folders by case-insensitive name or creates them at the end of the learner's list.</summary>
    private sealed class FolderResolver(ILearnerLibraryRepository library, Guid learnerId, DateTime now)
    {
        private Dictionary<string, Guid>? _byName;
        private int _order;

        public async Task<Guid> GetOrCreateAsync(string name, CancellationToken ct)
        {
            if (_byName is null)
            {
                var folders = await library.GetFoldersAsync(learnerId, ct);
                _byName = folders.ToDictionary(x => x.NormalizedName, x => x.Id);
                _order = folders.Count == 0 ? 0 : folders.Max(x => x.Order);
            }
            var folder = new VocabularyFolder { LearnerId = learnerId, CreatedAt = now };
            folder.Rename(name, now);
            if (_byName.TryGetValue(folder.NormalizedName, out var id)) return id;
            folder.Order = ++_order;
            library.AddFolder(folder);
            _byName[folder.NormalizedName] = folder.Id;
            return folder.Id;
        }
    }
}
