using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.AddLessonResource;

public sealed record AddLessonResourcesCommand(Guid CourseId, Guid ModuleId, Guid LessonId,
    Guid AdminId, IReadOnlyList<string> ObjectKeys) : IRequest<AddLessonResourcesResult>;
