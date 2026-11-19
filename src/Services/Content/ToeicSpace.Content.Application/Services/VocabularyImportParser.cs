using System.Text.Json;
using System.Text.RegularExpressions;
using ToeicSpace.Content.Application.DTOs.VocabularyImport;
using ToeicSpace.Content.Application.Models.VocabularyImport;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Services;

/// <summary>Parses and validates one stage file (data/vocabulary/README.md, schemaVersion 1).</summary>
public static partial class VocabularyImportParser
{
    public const int SupportedSchemaVersion = 1;
    public const int SlugMaxLength = 120;
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 1000;
    public const int HeadwordMaxLength = 100;
    public const int PhoneticMaxLength = 100;
    public const int MeaningMaxLength = 300;
    public const int TextMaxLength = 1000;
    public const int CefrMaxLength = 8;
    public const int MaxWordsPerSet = 100;
    public const int MaxSynonyms = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> CefrLevels = ["A1", "A2", "B1", "B2", "C1", "C2"];

    public static VocabularyStageFile Parse(VocabularyImportFile file)
    {
        VocabularyStageFile? parsed;
        try { parsed = JsonSerializer.Deserialize<VocabularyStageFile>(file.Content, JsonOptions); }
        catch (JsonException ex)
        { throw VocabularyErrors.InvalidImportFile(file.FileName, $"invalid JSON at line {ex.LineNumber + 1}."); }
        if (parsed is null) throw VocabularyErrors.InvalidImportFile(file.FileName, "the file is empty.");
        Validate(parsed, file.FileName);
        return parsed;
    }

    /// <summary>Tries the enum name only ("noun", "phrase"…), never a numeric value.</summary>
    public static bool TryParsePartOfSpeech(string? value, out PartOfSpeech partOfSpeech)
    {
        partOfSpeech = default;
        return !string.IsNullOrWhiteSpace(value) && !char.IsDigit(value.Trim()[0])
            && Enum.TryParse(value.Trim(), true, out partOfSpeech) && Enum.IsDefined(partOfSpeech);
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(VocabularyStageFile file, string fileName)
    {
        void Fail(string reason) => throw VocabularyErrors.InvalidImportFile(fileName, reason);

        if (file.SchemaVersion != SupportedSchemaVersion) Fail($"schemaVersion must be {SupportedSchemaVersion}.");
        var stage = file.Stage;
        if (stage is null) { Fail("stage is required."); return; }
        if (!IsSlug(stage.Slug)) Fail("stage.slug must be lowercase kebab-case.");
        if (!HasText(stage.Title, TitleMaxLength)) Fail("stage.title is required (max 200 characters).");
        if (stage.Description?.Length > DescriptionMaxLength) Fail("stage.description is too long.");
        if (stage.Order < 1) Fail("stage.order must be at least 1.");
        if (stage.ToeicTarget is < 0 or > 990) Fail("stage.toeicTarget must be between 0 and 990.");
        if (file.Sets is not { Count: > 0 }) { Fail("sets must not be empty."); return; }

        var slugs = new HashSet<string>();
        foreach (var set in file.Sets)
        {
            var where = $"set '{set.Slug}'";
            if (!IsSlug(set.Slug)) Fail($"{where}: slug must be lowercase kebab-case.");
            if (!slugs.Add(set.Slug!)) Fail($"{where}: duplicate slug.");
            if (!HasText(set.Title, TitleMaxLength)) Fail($"{where}: title is required (max 200 characters).");
            if (set.Topic?.Length > TitleMaxLength) Fail($"{where}: topic is too long.");
            if (set.Order < 1) Fail($"{where}: order must be at least 1.");
            if (set.Words is not { Count: > 0 } || set.Words.Count > MaxWordsPerSet)
            { Fail($"{where}: words must contain 1 to {MaxWordsPerSet} items."); return; }

            var keys = new HashSet<(string, PartOfSpeech)>();
            foreach (var word in set.Words)
            {
                var at = $"{where}, word '{word.Word}'";
                if (!HasText(word.Word, HeadwordMaxLength)) Fail($"{at}: word is required (max 100 characters).");
                if (!TryParsePartOfSpeech(word.PartOfSpeech, out var pos)) Fail($"{at}: unknown partOfSpeech.");
                if (!keys.Add((VocabularyWord.Normalize(word.Word!), pos))) Fail($"{at}: duplicate word in the set.");
                if (!HasText(word.MeaningVi, MeaningMaxLength)) Fail($"{at}: meaningVi is required (max 300 characters).");
                if (word.Phonetic?.Length > PhoneticMaxLength) Fail($"{at}: phonetic is too long.");
                if (word.DefinitionEn?.Length > TextMaxLength || word.Example?.Length > TextMaxLength
                    || word.ExampleVi?.Length > TextMaxLength) Fail($"{at}: definition or example is too long.");
                if (Clean(word.Cefr) is { } cefr && !CefrLevels.Contains(cefr.ToUpperInvariant()))
                    Fail($"{at}: cefr must be A1-C2.");
                if (word.Synonyms is { } synonyms && (synonyms.Count > MaxSynonyms
                    || synonyms.Any(x => !HasText(x, HeadwordMaxLength)))) Fail($"{at}: invalid synonyms.");
            }
        }
    }

    private static bool HasText(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;

    private static bool IsSlug(string? value) =>
        value is { Length: > 0 and <= SlugMaxLength } && SlugPattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
