using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateFolder;

public sealed record CreateFolderCommand(string Name, [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<FolderDto>;
