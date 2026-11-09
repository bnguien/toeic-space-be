using System.Text.Json;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.DictationImport;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Dictation;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.DictationImport.Commands.ImportDictationPacks;

/// <summary>
/// Upserts courses by <c>code</c> and sentences by <c>sourceCode</c>. Nothing is deleted: courses of the pack
/// missing from the file and sentences missing from their course are deactivated, so learner progress keeps its
/// sentence. A sentence whose content changes gets a new <c>UpdatedAt</c> (new word-proof salt).
/// </summary>
public sealed class ImportDictationPacksHandler(IDictationImportRepository repository, IUnitOfWork unitOfWork,
    TimeProvider clock, ILogger<ImportDictationPacksHandler> logger)
    : IRequestHandler<ImportDictationPacksCommand, DictationImportResultDto>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> Topics = DictationCatalog.Topics.Select(x => x.Key).ToHashSet();

    public async Task<DictationImportResultDto> Handle(ImportDictationPacksCommand request, CancellationToken ct)
    {
        var files = request.Files.Select(file => (file.FileName, Data: Parse(file))).ToArray();
        var duplicate = files.GroupBy(x => x.Data.Pack!).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw DictationErrors.InvalidImportFile(duplicate.Last().FileName, $"pack '{duplicate.Key}' is uploaded twice.");
        var results = new List<DictationPackImportResultDto>(files.Length);
        foreach (var (_, data) in files)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            var result = await ImportPackAsync(data, clock.GetUtcNow().UtcDateTime, ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation(
                "Imported dictation pack {Pack}: courses +{CoursesCreated}/~{CoursesUpdated}, items +{ItemsCreated}/~{ItemsUpdated}, deactivated {Deactivated}, unchanged {Unchanged}",
                result.Pack, result.CoursesCreated, result.CoursesUpdated, result.ItemsCreated, result.ItemsUpdated,
                result.Deactivated, result.Unchanged);
            results.Add(result);
        }
        return new DictationImportResultDto(results);
    }

    private async Task<DictationPackImportResultDto> ImportPackAsync(DictationPackFile file, DateTime now,
        CancellationToken ct)
    {
        var pack = file.Pack!.Trim();
        var sourceCourses = file.Courses!;
        var counts = new Counts();
        var courses = (await repository.GetCoursesForUpdateAsync(pack, sourceCourses.Select(x => x.Code!).ToArray(), ct))
            .ToDictionary(x => x.Code);
        var items = (await repository.GetItemsForUpdateAsync(
                sourceCourses.SelectMany(x => x.Items!).Select(x => x.SourceCode!).ToArray(), ct))
            .ToDictionary(x => x.SourceCode);
        var seenCourses = new HashSet<string>();

        foreach (var source in sourceCourses)
        {
            seenCourses.Add(source.Code!);
            if (!courses.TryGetValue(source.Code!, out var course))
            {
                course = new DictationCourse { Code = source.Code!, CreatedAt = now };
                Assign(course, pack, source);
                course.UpdatedAt = now;
                repository.AddCourse(course);
                courses[course.Code] = course;
                counts.CoursesCreated++;
            }
            else if (Assign(course, pack, source)) { course.UpdatedAt = now; counts.CoursesUpdated++; }
            else counts.Unchanged++;

            var seenItems = new HashSet<string>();
            foreach (var sourceItem in source.Items!)
            {
                seenItems.Add(sourceItem.SourceCode!);
                if (!items.TryGetValue(sourceItem.SourceCode!, out var item))
                {
                    item = new DictationItem { SourceCode = sourceItem.SourceCode!, CreatedAt = now };
                    Assign(item, course, source.Level, sourceItem);
                    item.UpdatedAt = now;
                    repository.AddItem(item);
                    items[item.SourceCode] = item;
                    counts.ItemsCreated++;
                }
                else if (Assign(item, course, source.Level, sourceItem)) { item.UpdatedAt = now; counts.ItemsUpdated++; }
                else counts.Unchanged++;
            }
            foreach (var removed in course.Items.Where(x => x.IsActive && !seenItems.Contains(x.SourceCode)).ToList())
            {
                removed.IsActive = false;
                removed.UpdatedAt = now;
                counts.Deactivated++;
            }
        }
        foreach (var removed in courses.Values.Where(x => x.Pack == pack && x.IsActive && !seenCourses.Contains(x.Code)))
        {
            removed.IsActive = false;
            removed.UpdatedAt = now;
            foreach (var item in removed.Items.Where(x => x.IsActive)) { item.IsActive = false; item.UpdatedAt = now; }
            counts.Deactivated++;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return new DictationPackImportResultDto(pack, counts.CoursesCreated, counts.CoursesUpdated, counts.ItemsCreated,
            counts.ItemsUpdated, counts.Deactivated, counts.Unchanged);
    }

    private static bool Assign(DictationCourse course, string pack, DictationPackFileCourse source) =>
        Set(course.Pack, pack, v => course.Pack = v)
        | Set(course.Level, source.Level, v => course.Level = v)
        | Set(course.Order, source.Order, v => course.Order = v)
        | Set(course.Title, source.Title!.Trim(), v => course.Title = v)
        | Set(course.ItemCount, source.Items!.Count, v => course.ItemCount = v)
        | Set(course.WordsPerSentence, Math.Round(source.AvgWords, 1), v => course.WordsPerSentence = v)
        | Set(course.VocabCount, source.VocabCount, v => course.VocabCount = v)
        | Set(course.IsActive, true, v => course.IsActive = v);

    private static bool Assign(DictationItem item, DictationCourse course, int level, DictationPackFileItem source)
    {
        var keywords = (source.Keywords ?? []).Select(k => new DictationKeyword(Text(k.Word), Text(k.Meaning),
            Text(k.PartOfSpeech), Text(k.Ipa), Text(k.Cefr))).ToList();
        var notes = (source.Notes ?? []).Select((n, i) => new DictationNote(Text(n.Id) is { Length: > 0 } id ? id : $"n{i}",
            Text(n.Text), n.Type == nameof(DictationNoteType.Phrase) ? DictationNoteType.Phrase : DictationNoteType.Word,
            Text(n.Explanation))).ToList();
        var vocab = (source.Vocab ?? []).Select(v => new DictationVocabEntry(Text(v.Word), Text(v.Lemma), Text(v.Meaning),
            Text(v.PartOfSpeech), Text(v.Ipa))).ToList();
        var changed = false;
        if (!ReferenceEquals(item.Course, course) && (course.Id == 0 || item.CourseId != course.Id))
        {
            item.Course = course;
            changed = true;
        }
        changed |= Set(item.Order, source.Order, v => item.Order = v)
            | Set(item.Level, level, v => item.Level = v)
            | Set(item.Topic, Topics.Contains(Text(source.Topic)) ? Text(source.Topic) : "office_hr", v => item.Topic = v)
            | Set(item.Text, source.Text!.Trim(), v => item.Text = v)
            | Set(item.Translation, Text(source.Translation), v => item.Translation = v)
            | Set(item.Ipa, Text(source.Ipa), v => item.Ipa = v)
            | Set(item.VoiceKey, SpeechVoices.All.Any(x => x.Key == source.Voice) ? source.Voice! : string.Empty,
                v => item.VoiceKey = v)
            | Set(item.Rate, SpeechVoices.ClampRate(source.Rate), v => item.Rate = v)
            | Set(item.MaxPlays, source.MaxPlays, v => item.MaxPlays = v)
            | Set(item.IsActive, true, v => item.IsActive = v);
        if (!item.Keywords.SequenceEqual(keywords)) { item.Keywords = keywords; changed = true; }
        if (!item.Notes.SequenceEqual(notes)) { item.Notes = notes; changed = true; }
        if (!item.Vocab.SequenceEqual(vocab)) { item.Vocab = vocab; changed = true; }
        return changed;
    }

    private static string Text(string? value) => value?.Trim() ?? string.Empty;

    private static bool Set<T>(T current, T value, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return false;
        assign(value);
        return true;
    }

    private static DictationPackFile Parse(DictationImportFile file)
    {
        DictationPackFile? data;
        try { data = JsonSerializer.Deserialize<DictationPackFile>(file.Content, Json); }
        catch (JsonException ex) { throw DictationErrors.InvalidImportFile(file.FileName, $"invalid JSON ({ex.Message})."); }
        if (data is null || data.SchemaVersion != 1)
            throw DictationErrors.InvalidImportFile(file.FileName, "schemaVersion must be 1.");
        if (string.IsNullOrWhiteSpace(data.Pack) || data.Pack.Length > 40 || data.Courses is not { Count: > 0 })
            throw DictationErrors.InvalidImportFile(file.FileName, "pack and courses are required.");
        var codes = new HashSet<string>();
        var sourceCodes = new HashSet<string>();
        foreach (var course in data.Courses)
        {
            if (string.IsNullOrWhiteSpace(course.Code) || course.Code.Length > 40 || !codes.Add(course.Code))
                throw DictationErrors.InvalidImportFile(file.FileName, $"course code '{course.Code}' is missing, too long or repeated.");
            if (string.IsNullOrWhiteSpace(course.Title) || course.Title.Length > 200 || !DictationCatalog.IsLevel(course.Level))
                throw DictationErrors.InvalidImportFile(file.FileName, $"course '{course.Code}' needs a title and a level 1-5.");
            if (course.Items is not { Count: > 0 })
                throw DictationErrors.InvalidImportFile(file.FileName, $"course '{course.Code}' has no sentences.");
            foreach (var item in course.Items)
            {
                if (string.IsNullOrWhiteSpace(item.SourceCode) || item.SourceCode.Length > 40 || !sourceCodes.Add(item.SourceCode))
                    throw DictationErrors.InvalidImportFile(file.FileName, $"sentence code '{item.SourceCode}' is missing, too long or repeated.");
                if (string.IsNullOrWhiteSpace(item.Text) || item.Text.Length > 1000 || DictationGrader.Tokens(item.Text).Count == 0)
                    throw DictationErrors.InvalidImportFile(file.FileName, $"sentence '{item.SourceCode}' has no text.");
            }
        }
        return data;
    }

    private sealed class Counts
    {
        public int CoursesCreated { get; set; }
        public int CoursesUpdated { get; set; }
        public int ItemsCreated { get; set; }
        public int ItemsUpdated { get; set; }
        public int Deactivated { get; set; }
        public int Unchanged { get; set; }
    }
}
