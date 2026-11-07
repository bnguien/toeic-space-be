namespace ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

/// <summary><paramref name="Created"/> is false when the word was already in the library (folder/note updated).</summary>
public sealed record SavedWordUpsertResult(SavedWordDto SavedWord, bool Created);
