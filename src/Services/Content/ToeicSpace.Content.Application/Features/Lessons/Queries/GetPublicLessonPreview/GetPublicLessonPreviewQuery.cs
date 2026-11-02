using ToeicSpace.Content.Application.DTOs.Courses;

namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetPublicLessonPreview;

public sealed record GetPublicLessonPreviewQuery(Guid CourseId, Guid LessonId) : IRequest<PublicLessonPreviewDto>;
