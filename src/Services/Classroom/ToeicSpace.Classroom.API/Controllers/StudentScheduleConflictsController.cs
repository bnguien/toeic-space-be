using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Features.StudentConflicts.Queries;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Route("api/v1/students/me/schedule-conflicts")]
[Authorize(Roles = "User")]
public sealed class StudentScheduleConflictsController(ISender sender) : ControllerBase
{
    private Guid StudentId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ClassRuleException("invalid_subject", "Invalid user identity.", 401);

    // Explicit on-demand read; the ordinary Session Calendar endpoint does not invoke conflict detection.
    [HttpGet]
    public Task<StudentConflictPreview> Mine([FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] int? detailsPage, [FromQuery] int detailsPageSize = 20, CancellationToken ct = default) =>
        sender.Send(new GetMyStudentConflictsQuery(StudentId, from, to,
            detailsPage is { } page ? new ConflictDetailsRequest(page, detailsPageSize) : null), ct);
}
