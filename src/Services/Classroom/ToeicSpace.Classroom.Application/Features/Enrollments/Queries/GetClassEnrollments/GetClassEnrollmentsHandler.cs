using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Enrollments;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Queries.GetClassEnrollments;

public sealed class GetClassEnrollmentsHandler(IClassRepository classes, IEnrollmentRepository enrollments,
    ILogger<GetClassEnrollmentsHandler> logger)
    : IRequestHandler<GetClassEnrollmentsQuery, PagedResult<ClassEnrollmentDto>>
{
    public async Task<PagedResult<ClassEnrollmentDto>> Handle(GetClassEnrollmentsQuery request, CancellationToken ct)
    {
        // Admin may inspect preserved enrollment history of soft-deleted classes before permanent deletion.
        if (await classes.GetByIdAsync(request.ClassId, true, ct) is null)
            throw ClassRuleException.NotFound();
        var page = await enrollments.ListAsync(request.ClassId, request.Status, request.StudentId,
            request.Page, request.PageSize, ct);
        logger.LogDebug("Listed enrollments for class {ClassId}: status {Status}, student {StudentId}, page {Page}, returned {Count} of {TotalCount}",
            request.ClassId, request.Status, request.StudentId, request.Page, page.Items.Count, page.TotalCount);
        return new PagedResult<ClassEnrollmentDto>(page.Items.Select(x =>
            x.Enrollment.ToDto(x.StudentName, x.StudentEmail)).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }
}
