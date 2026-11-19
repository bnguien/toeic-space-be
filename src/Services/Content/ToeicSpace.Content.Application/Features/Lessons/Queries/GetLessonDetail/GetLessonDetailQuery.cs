using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessonDetail;

public sealed record GetLessonDetailQuery(Guid CourseId, Guid ModuleId, Guid LessonId) : IRequest<LessonDto>;
