using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.GrammarImport.Commands.ImportGrammarContent;

public sealed class ImportGrammarContentHandler(IGrammarContentRepository repository, IUnitOfWork unitOfWork,
    GrammarContentCache cache, TimeProvider clock, ILogger<ImportGrammarContentHandler> logger)
    : IRequestHandler<ImportGrammarContentCommand, GrammarImportResultDto>
{
    public const int SchemaVersion = 1;
    private static readonly Regex Key = new(@"^\d{2}$", RegexOptions.Compiled);

    public async Task<GrammarImportResultDto> Handle(ImportGrammarContentCommand request, CancellationToken ct)
    {
        var incoming = new Dictionary<(GrammarDocumentKind, string), string>();
        int topics = 0, stages = 0;
        foreach (var file in request.Files)
        {
            var bundle = ReadBundle(file);
            foreach (var topic in bundle["topics"] as JsonArray ?? [])
            {
                var key = KeyOf(file, topic, "topic");
                foreach (var (kind, field) in new[]
                         {
                             (GrammarDocumentKind.Knowledge, "knowledge"), (GrammarDocumentKind.Vocab, "vocab"),
                             (GrammarDocumentKind.Exercises, "exercises")
                         })
                    incoming[(kind, key)] = Document(file, topic![field], $"topic {key} {field}");
                topics++;
            }
            foreach (var stage in bundle["stages"] as JsonArray ?? [])
            {
                incoming[(GrammarDocumentKind.StageTest, KeyOf(file, stage, "stage"))] = Document(file, stage, "stage test");
                stages++;
            }
        }
        if (incoming.Count == 0) throw GrammarErrors.InvalidImportFile(request.Files[0].FileName, "no topics or stages.");

        var now = clock.GetUtcNow().UtcDateTime;
        var existing = (await repository.ListForUpdateAsync(ct)).ToDictionary(d => (d.Kind, d.Key));
        int created = 0, updated = 0, unchanged = 0;
        foreach (var ((kind, key), json) in incoming)
        {
            var hash = GrammarContentBuilder.Hash(json);
            if (existing.TryGetValue((kind, key), out var doc))
            {
                if (doc.ContentHash == hash) { unchanged++; continue; }
                doc.Content = json;
                doc.ContentHash = hash;
                doc.UpdatedAt = now;
                updated++;
                continue;
            }
            doc = new GrammarDocument { Kind = kind, Key = key, Content = json, ContentHash = hash, ImportedAt = now, UpdatedAt = now };
            repository.Add(doc);
            existing[(kind, key)] = doc;
            created++;
        }
        // Validate the resulting content as a whole (missing parts, duplicate question ids) before saving.
        var built = GrammarContentBuilder.Build(existing.Values.ToList());
        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidate();
        logger.LogInformation("Imported grammar content: {Topics} topics, {Stages} stage tests, +{Created} ~{Updated} ={Unchanged}",
            topics, stages, created, updated, unchanged);
        return new GrammarImportResultDto(topics, stages, created, updated, unchanged, built.Questions.Count);
    }

    private static JsonObject ReadBundle(GrammarImportFile file)
    {
        byte[] raw = file.Content;
        if (raw.Length > 2 && raw[0] == 0x1f && raw[1] == 0x8b)
        {
            using var input = new GZipStream(new MemoryStream(raw), CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                if (output.Length > ImportGrammarContentCommand.MaxUncompressedBytes)
                    throw GrammarErrors.InvalidImportFile(file.FileName, "uncompressed content is too large.");
            }
            raw = output.ToArray();
        }
        JsonNode? node;
        try { node = JsonNode.Parse(raw); }
        catch (JsonException) { throw GrammarErrors.InvalidImportFile(file.FileName, "unreadable JSON."); }
        if (node is not JsonObject bundle) throw GrammarErrors.InvalidImportFile(file.FileName, "a JSON object is expected.");
        if (bundle["schemaVersion"]?.GetValue<int>() != SchemaVersion)
            throw GrammarErrors.InvalidImportFile(file.FileName, $"schemaVersion must be {SchemaVersion}.");
        return bundle;
    }

    private static string KeyOf(GrammarImportFile file, JsonNode? item, string field) =>
        item?[field] is JsonValue v && v.TryGetValue<string>(out var key) && Key.IsMatch(key) ? key
            : throw GrammarErrors.InvalidImportFile(file.FileName, $"every item needs a two-digit '{field}'.");

    private static string Document(GrammarImportFile file, JsonNode? node, string what) =>
        node is JsonObject obj ? obj.ToJsonString(GrammarContentBuilder.PublicJson)
            : throw GrammarErrors.InvalidImportFile(file.FileName, $"{what} must be a JSON object.");
}
