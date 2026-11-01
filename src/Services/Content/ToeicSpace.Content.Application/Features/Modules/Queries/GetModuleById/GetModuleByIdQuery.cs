using ToeicSpace.Content.Application.DTOs.Modules;

namespace ToeicSpace.Content.Application.Features.Modules.Queries.GetModuleById;

public sealed record GetModuleByIdQuery(Guid CourseId, Guid ModuleId) : IRequest<ModuleDto>;
