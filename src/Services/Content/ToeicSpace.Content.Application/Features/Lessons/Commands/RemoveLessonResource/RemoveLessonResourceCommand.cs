namespace ToeicSpace.Content.Application.Features.Lessons.Commands.RemoveLessonResource;

public sealed record RemoveLessonResourceCommand(Guid CourseId, Guid ModuleId, Guid LessonId, Guid ResourceId) : IRequest;
