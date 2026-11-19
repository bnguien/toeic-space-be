using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.PublishLesson;

public sealed record PublishLessonCommand(Guid CourseId, Guid ModuleId, Guid LessonId) : IRequest<LessonDto>;
