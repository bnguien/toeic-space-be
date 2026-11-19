using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.Courses.Queries.GetCourses;

public sealed record GetCoursesQuery(string? Search, CourseStatus? Status, CourseLevel? Level,
    CourseExamType? ExamType, int Page = 1, int PageSize = 20) : IRequest<PagedResult<CourseDto>>;
