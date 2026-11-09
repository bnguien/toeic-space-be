namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.RemoveSavedWord;

/// <summary>
/// Removes a word from the library. An official word keeps its progress; a custom word is deleted together with its
/// progress and review logs.
/// </summary>
public sealed record RemoveSavedWordCommand(Guid SavedWordId, Guid LearnerId) : IRequest;
