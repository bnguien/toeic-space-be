using ToeicSpace.Content.Application.DTOs.Courses;

namespace ToeicSpace.Content.Application.Features.Courses.Queries.GetPublicCourse;

public sealed record GetPublicCourseQuery(Guid Id) : IRequest<PublicCourseDetailDto>;
