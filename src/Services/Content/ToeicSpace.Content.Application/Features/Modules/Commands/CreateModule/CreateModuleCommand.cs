using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Modules;

namespace ToeicSpace.Content.Application.Features.Modules.Commands.CreateModule;

public sealed record CreateModuleCommand(string Title, string? Description, int? SortOrder) : IRequest<ModuleDto>
{
    [JsonIgnore] public Guid CourseId { get; set; }
}
