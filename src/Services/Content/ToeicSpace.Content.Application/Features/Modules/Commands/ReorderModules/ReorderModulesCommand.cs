using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Modules;

namespace ToeicSpace.Content.Application.Features.Modules.Commands.ReorderModules;

public sealed record ReorderModulesCommand(IReadOnlyList<Guid> ModuleIds) : IRequest<IReadOnlyList<ModuleDto>>
{
    [JsonIgnore] public Guid CourseId { get; set; }
}
