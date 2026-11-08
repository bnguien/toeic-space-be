namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.DeleteFolder;

/// <summary>Deletes a folder; its words stay in the library as unfiled.</summary>
public sealed record DeleteFolderCommand(Guid FolderId, Guid LearnerId) : IRequest;
