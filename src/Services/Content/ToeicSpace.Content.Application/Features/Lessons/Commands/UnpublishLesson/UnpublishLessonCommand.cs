using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.UnpublishLesson;

public sealed record UnpublishLessonCommand(Guid CourseId, Guid ModuleId, Guid LessonId) : IRequest<LessonDto>;
