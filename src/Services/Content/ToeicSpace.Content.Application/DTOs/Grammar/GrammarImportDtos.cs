namespace ToeicSpace.Content.Application.DTOs.Grammar;

public sealed record GrammarImportFile(string FileName, string? ContentType, long SizeBytes, byte[] Content);

public sealed record GrammarImportResultDto(int Topics, int StageTests, int DocumentsCreated, int DocumentsUpdated,
    int Unchanged, int Questions);
