using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.VocabularyImport;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.VocabularyImport;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyImport.Commands.ImportVocabularyStages;

/// <summary>
/// Upserts stages (by slug), sets (by stage + slug) and words (by normalized headword + part of speech).
/// Nothing is ever deleted: sets missing from a file are deactivated and words stay in the bank, so learner
/// progress and review logs always keep their word.
/// </summary>
public sealed class ImportVocabularyStagesHandler(IVocabularyImportRepository repository, IUnitOfWork unitOfWork,
    TimeProvider clock, ILogger<ImportVocabularyStagesHandler> logger)
    : IRequestHandler<ImportVocabularyStagesCommand, VocabularyImportResultDto>
{
    public async Task<VocabularyImportResultDto> Handle(ImportVocabularyStagesCommand request, CancellationToken ct)
    {
        var files = request.Files.Select(file => (file.FileName, Data: VocabularyImportParser.Parse(file))).ToArray();
        var duplicate = files.GroupBy(x => x.Data.Stage!.Slug).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw VocabularyErrors.InvalidImportFile(duplicate.Last().FileName, $"stage '{duplicate.Key}' is uploaded twice.");

        var now = clock.GetUtcNow().UtcDateTime;
        var run = new ImportRun(now);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var results = new List<StageImportResultDto>(files.Length);
        foreach (var (_, data) in files)
        {
            var result = await ImportStageAsync(data, run, ct);
            logger.LogInformation(
                "Imported vocabulary stage {Slug}: sets +{SetsCreated}/~{SetsUpdated}, words +{WordsCreated}/~{WordsUpdated}, unchanged {Unchanged}",
                result.Slug, result.SetsCreated, result.SetsUpdated, result.WordsCreated, result.WordsUpdated,
                result.Unchanged);
            results.Add(result);
        }
        await transaction.CommitAsync(ct);
        return new VocabularyImportResultDto(results);
    }

    private async Task<StageImportResultDto> ImportStageAsync(VocabularyStageFile file, ImportRun run,
        CancellationToken ct)
    {
        var source = file.Stage!;
        var counts = new StageCounts();
        var stage = await repository.GetStageForUpdateAsync(source.Slug!, ct);
        if (stage is null)
        {
            stage = new VocabularyStage { Slug = source.Slug!, CreatedAt = run.Now };
            repository.AddStage(stage);
        }
        if (Assign(stage, source)) stage.UpdatedAt = run.Now;

        var words = await ResolveWordsAsync(file, run, counts, ct);
        var existingSets = stage.Sets.ToDictionary(x => x.Slug);
        foreach (var sourceSet in file.Sets!)
        {
            var wordIds = sourceSet.Words!.Select(w => words[Key(w)].Id).ToArray();
            if (!existingSets.Remove(sourceSet.Slug!, out var set))
            {
                set = new VocabularySet { StageId = stage.Id, Slug = sourceSet.Slug!, CreatedAt = run.Now };
                Assign(set, sourceSet, run.Now);
                repository.AddSet(set);
                for (var i = 0; i < wordIds.Length; i++)
                    repository.AddSetWord(new VocabularySetWord { SetId = set.Id, WordId = wordIds[i], Order = i + 1 });
                counts.SetsCreated++;
                continue;
            }
            var changed = Assign(set, sourceSet, run.Now);
            changed |= await RelinkAsync(set, wordIds, ct);
            if (changed) { set.UpdatedAt = run.Now; counts.SetsUpdated++; }
            else counts.Unchanged++;
        }
        // Sets no longer in the file are hidden, never deleted.
        foreach (var removed in existingSets.Values.Where(x => x.IsActive))
        {
            removed.IsActive = false;
            removed.UpdatedAt = run.Now;
            counts.SetsUpdated++;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return new StageImportResultDto(stage.Slug, counts.SetsCreated, counts.SetsUpdated, counts.WordsCreated,
            counts.WordsUpdated, counts.Unchanged);
    }

    /// <summary>Finds or creates every word of the file; each word is counted once per import call.</summary>
    private async Task<Dictionary<(string, PartOfSpeech), VocabularyWord>> ResolveWordsAsync(VocabularyStageFile file,
        ImportRun run, StageCounts counts, CancellationToken ct)
    {
        var sources = file.Sets!.SelectMany(x => x.Words!).GroupBy(Key).ToDictionary(x => x.Key, x => x.Last());
        var unknown = sources.Keys.Where(k => !run.Words.ContainsKey(k)).Select(k => k.Item1).Distinct().ToArray();
        if (unknown.Length > 0)
            foreach (var word in await repository.GetWordsForUpdateAsync(unknown, ct))
                run.Words.TryAdd((word.NormalizedHeadword, word.PartOfSpeech), word);

        foreach (var (key, source) in sources)
        {
            if (!run.Processed.Add(key)) continue;
            if (!run.Words.TryGetValue(key, out var word))
            {
                word = new VocabularyWord { NormalizedHeadword = key.Item1, PartOfSpeech = key.Item2, CreatedAt = run.Now };
                Assign(word, source);
                word.UpdatedAt = run.Now;
                repository.AddWord(word);
                run.Words[key] = word;
                counts.WordsCreated++;
            }
            else if (Assign(word, source))
            {
                word.UpdatedAt = run.Now;
                counts.WordsUpdated++;
            }
            else counts.Unchanged++;
        }
        return sources.Keys.ToDictionary(k => k, k => run.Words[k]);
    }

    /// <summary>Makes the set's links match <paramref name="wordIds"/> without breaking the (SetId, Order) index.</summary>
    private async Task<bool> RelinkAsync(VocabularySet set, Guid[] wordIds, CancellationToken ct)
    {
        var current = set.Words.OrderBy(x => x.Order).ToList();
        if (current.Select(x => x.WordId).SequenceEqual(wordIds)) return false;

        var wanted = wordIds.ToHashSet();
        foreach (var link in current.Where(x => !wanted.Contains(x.WordId)))
        {
            repository.RemoveSetWord(link);
            set.Words.Remove(link);
        }
        var kept = set.Words.ToDictionary(x => x.WordId);
        // Park kept links on distinct negative positions first, so no two rows ever share an order.
        var parking = -1;
        foreach (var link in kept.Values) link.Order = parking--;
        await unitOfWork.SaveChangesAsync(ct);
        for (var i = 0; i < wordIds.Length; i++)
        {
            if (kept.TryGetValue(wordIds[i], out var link)) link.Order = i + 1;
            else repository.AddSetWord(new VocabularySetWord { SetId = set.Id, WordId = wordIds[i], Order = i + 1 });
        }
        return true;
    }

    private static (string, PartOfSpeech) Key(VocabularyStageFileWord word)
    {
        VocabularyImportParser.TryParsePartOfSpeech(word.PartOfSpeech, out var pos);
        return (VocabularyWord.Normalize(word.Word!), pos);
    }

    private static bool Assign(VocabularyStage stage, VocabularyStageFileStage source)
    {
        var changed = Set(stage.Title, source.Title!.Trim(), v => stage.Title = v)
            | Set(stage.Description, VocabularyImportParser.Clean(source.Description), v => stage.Description = v)
            | Set(stage.Order, source.Order, v => stage.Order = v)
            | Set(stage.ToeicTarget, source.ToeicTarget, v => stage.ToeicTarget = v)
            | Set(stage.IsActive, true, v => stage.IsActive = v);
        return changed;
    }

    private static bool Assign(VocabularySet set, VocabularyStageFileSet source, DateTime now)
    {
        var changed = Set(set.Title, source.Title!.Trim(), v => set.Title = v)
            | Set(set.Topic, VocabularyImportParser.Clean(source.Topic), v => set.Topic = v)
            | Set(set.Order, source.Order, v => set.Order = v)
            | Set(set.IsActive, true, v => set.IsActive = v);
        if (changed) set.UpdatedAt = now;
        return changed;
    }

    private static bool Assign(VocabularyWord word, VocabularyStageFileWord source)
    {
        var synonyms = (source.Synonyms ?? []).Select(x => x.Trim()).ToList();
        var changed = Set(word.Headword, source.Word!.Trim(), v => word.Headword = v)
            | Set(word.Phonetic, source.Phonetic?.Trim() ?? string.Empty, v => word.Phonetic = v)
            | Set(word.MeaningVi, source.MeaningVi!.Trim(), v => word.MeaningVi = v)
            | Set(word.DefinitionEn, VocabularyImportParser.Clean(source.DefinitionEn), v => word.DefinitionEn = v)
            | Set(word.Example, VocabularyImportParser.Clean(source.Example), v => word.Example = v)
            | Set(word.ExampleVi, VocabularyImportParser.Clean(source.ExampleVi), v => word.ExampleVi = v)
            | Set(word.Cefr, VocabularyImportParser.Clean(source.Cefr)?.ToUpperInvariant(), v => word.Cefr = v);
        if (!word.Synonyms.SequenceEqual(synonyms)) { word.Synonyms = synonyms; changed = true; }
        return changed;
    }

    private static bool Set<T>(T current, T value, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return false;
        assign(value);
        return true;
    }

    private sealed class ImportRun(DateTime now)
    {
        public DateTime Now { get; } = now;
        public Dictionary<(string, PartOfSpeech), VocabularyWord> Words { get; } = [];
        public HashSet<(string, PartOfSpeech)> Processed { get; } = [];
    }

    private sealed class StageCounts
    {
        public int SetsCreated { get; set; }
        public int SetsUpdated { get; set; }
        public int WordsCreated { get; set; }
        public int WordsUpdated { get; set; }
        public int Unchanged { get; set; }
    }
}
