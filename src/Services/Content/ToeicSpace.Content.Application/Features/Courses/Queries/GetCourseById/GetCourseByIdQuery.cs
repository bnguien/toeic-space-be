using ToeicSpace.Content.Application.DTOs.Courses;

namespace ToeicSpace.Content.Application.Features.Courses.Queries.GetCourseById;

public sealed record GetCourseByIdQuery(Guid CourseId) : IRequest<CourseDto>;
