using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Enrollments;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Queries.GetTeacherClassStudents;

public sealed class GetTeacherClassStudentsHandler(IClassRepository classes, IEnrollmentRepository enrollments)
    : IRequestHandler<GetTeacherClassStudentsQuery, PagedResult<ClassEnrollmentDto>>
{
    public async Task<PagedResult<ClassEnrollmentDto>> Handle(GetTeacherClassStudentsQuery request, CancellationToken ct)
    {
        var classroom = await classes.GetDetailAsync(request.ClassId, ct) ?? throw ClassRuleException.NotFound();
        if (!classroom.Instructors.Any(x => x.TeacherId == request.TeacherId && x.UnassignedAt == null))
            throw new ClassRuleException("forbidden_class", "You are not assigned to this class.", 403);

        var page = await enrollments.ListAsync(request.ClassId, EnrollmentStatus.Active, null,
            request.Page, request.PageSize, ct);
        return new PagedResult<ClassEnrollmentDto>(page.Items.Select(x =>
            x.Enrollment.ToDto(x.StudentName, x.StudentEmail)).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }
}
