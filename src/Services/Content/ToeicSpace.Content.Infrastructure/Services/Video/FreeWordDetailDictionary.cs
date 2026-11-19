using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Infrastructure.Services.Video;

/// <summary>
/// Word card details from dictionaryapi.dev (Studychill <c>word_detail._from_dictionary_api</c>): synonyms/antonyms of
/// every sense, one definition per part of speech (≤ 3), US IPA (phonetic with a <c>-us.mp3</c> audio first).
/// Null = the source failed (cache briefly); <see cref="WordDetailResult.NotFound"/> = unknown word.
/// </summary>
public sealed class FreeWordDetailDictionary(HttpClient http, ILogger<FreeWordDetailDictionary> logger) : IWordDetailDictionary
{
    public const int MaxDefinitions = 3;

    public async Task<WordDetailResult?> LookupAsync(string word, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await http.GetAsync("https://api.dictionaryapi.dev/api/v2/entries/en/"
                + Uri.EscapeDataString(word), timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound) return WordDetailResult.NotFound;
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            return Parse(document.RootElement, word);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                       || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogInformation("Word detail of {Word} failed: {Error}", word, ex.GetType().Name);
            return null;
        }
    }

    public static WordDetailResult Parse(JsonElement root, string word)
    {
        var synonyms = new List<string>();
        var antonyms = new List<string>();
        var senses = new List<WordSense>();
        string ipa = string.Empty, partOfSpeech = string.Empty;
        if (root.ValueKind != JsonValueKind.Array) return WordDetailResult.NotFound;
        foreach (var entry in root.EnumerateArray())
        {
            if (ipa.Length == 0) ipa = UsIpa(entry);
            foreach (var meaning in Items(entry, "meanings"))
            {
                var pos = Text(meaning, "partOfSpeech", 24);
                if (partOfSpeech.Length == 0) partOfSpeech = pos;
                foreach (var s in Items(meaning, "synonyms")) WordDetailRules.AddUnique(synonyms, s.GetString(), word);
                foreach (var a in Items(meaning, "antonyms")) WordDetailRules.AddUnique(antonyms, a.GetString(), word);
                foreach (var definition in Items(meaning, "definitions"))
                {
                    foreach (var s in Items(definition, "synonyms")) WordDetailRules.AddUnique(synonyms, s.GetString(), word);
                    foreach (var a in Items(definition, "antonyms")) WordDetailRules.AddUnique(antonyms, a.GetString(), word);
                    var text = Text(definition, "definition", 220);
                    if (text.Length > 0 && senses.Count < MaxDefinitions && senses.All(x => x.PartOfSpeech != pos))
                        senses.Add(new WordSense(pos, text, Text(definition, "example", 200)));
                }
            }
        }
        return new WordDetailResult(synonyms, antonyms, senses, ipa, partOfSpeech);
    }

    private static string UsIpa(JsonElement entry)
    {
        var phonetics = Items(entry, "phonetics").Where(p => Text(p, "text", 60).Length > 0).ToList();
        foreach (var p in phonetics)
            if (Text(p, "audio", 300).EndsWith("-us.mp3", StringComparison.Ordinal)) return Text(p, "text", 60);
        var phonetic = Text(entry, "phonetic", 60);
        return phonetic.Length > 0 ? phonetic : phonetics.Count > 0 ? Text(phonetics[0], "text", 60) : string.Empty;
    }

    private static IEnumerable<JsonElement> Items(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray() : [];

    private static string Text(JsonElement e, string name, int max)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return string.Empty;
        var text = v.GetString()!.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
