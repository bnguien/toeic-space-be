using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.Courses.Queries.BrowsePublicCourses;

public sealed record BrowsePublicCoursesQuery(string? Search, CourseExamType? ExamType, CourseLevel? Level,
    int? TargetScore, int Page = 1, int PageSize = 20) : IRequest<PagedResult<PublicCourseSummaryDto>>;
