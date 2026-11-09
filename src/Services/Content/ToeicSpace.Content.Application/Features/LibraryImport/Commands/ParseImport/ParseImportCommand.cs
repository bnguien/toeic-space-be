using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.LibraryImport;

namespace ToeicSpace.Content.Application.Features.LibraryImport.Commands.ParseImport;

/// <summary>Parses pasted text or JSON into a preview; nothing is stored.</summary>
public sealed record ParseImportCommand(string? Text = null, string? Json = null,
    [property: JsonIgnore] Guid LearnerId = default) : IRequest<ImportPreviewDto>
{
    public const int MaxJsonLength = 200_000;
}
