using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessons;

public sealed record GetLessonsQuery(Guid CourseId, Guid ModuleId) : IRequest<IReadOnlyList<LessonSummaryDto>>;
