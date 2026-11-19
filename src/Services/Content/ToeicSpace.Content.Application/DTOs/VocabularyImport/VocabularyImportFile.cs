namespace ToeicSpace.Content.Application.DTOs.VocabularyImport;

/// <summary>One uploaded stage file, read by the API into memory (at most a few MB).</summary>
public sealed record VocabularyImportFile(string FileName, string? ContentType, long SizeBytes, string Content);
