using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Enrollments;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Queries.GetClassEnrollments;

public sealed record GetClassEnrollmentsQuery(Guid ClassId, EnrollmentStatus? Status, Guid? StudentId,
    int Page = 1, int PageSize = 20) : IRequest<PagedResult<ClassEnrollmentDto>>;
