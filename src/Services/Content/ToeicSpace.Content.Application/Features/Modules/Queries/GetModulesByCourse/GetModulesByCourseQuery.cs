using ToeicSpace.Content.Application.DTOs.Modules;

namespace ToeicSpace.Content.Application.Features.Modules.Queries.GetModulesByCourse;

public sealed record GetModulesByCourseQuery(Guid CourseId) : IRequest<IReadOnlyList<ModuleDto>>;
