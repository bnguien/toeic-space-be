using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Modules;

namespace ToeicSpace.Content.Application.Features.Modules.Commands.UpdateModule;

public sealed record UpdateModuleCommand(string Title, string? Description) : IRequest<ModuleDto>
{
    [JsonIgnore] public Guid CourseId { get; set; }
    [JsonIgnore] public Guid ModuleId { get; set; }
}
