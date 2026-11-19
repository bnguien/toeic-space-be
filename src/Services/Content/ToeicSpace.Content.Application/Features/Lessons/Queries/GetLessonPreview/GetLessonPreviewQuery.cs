using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessonPreview;

public sealed record GetLessonPreviewQuery(Guid CourseId, Guid ModuleId, Guid LessonId) : IRequest<LessonPreviewDto>;
