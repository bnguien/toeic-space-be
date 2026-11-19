using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Enrollments;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Queries.GetTeacherClassStudents;

public sealed record GetTeacherClassStudentsQuery(Guid ClassId, Guid TeacherId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ClassEnrollmentDto>>;
