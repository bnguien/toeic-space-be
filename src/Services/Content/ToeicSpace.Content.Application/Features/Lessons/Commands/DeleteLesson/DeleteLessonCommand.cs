namespace ToeicSpace.Content.Application.Features.Lessons.Commands.DeleteLesson;

public sealed record DeleteLessonCommand(Guid CourseId, Guid ModuleId, Guid LessonId) : IRequest;
