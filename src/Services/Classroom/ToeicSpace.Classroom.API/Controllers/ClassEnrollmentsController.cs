using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Enrollments;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.EnrollStudent;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.BulkEnrollStudents;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.ImportStudents;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.WithdrawStudent;
using ToeicSpace.Classroom.Application.Features.Enrollments.Queries.GetClassEnrollments;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Route("api/v1/classes/{classId:guid}/enrollments")]
[Authorize(Policy = "Admin")]
public sealed class ClassEnrollmentsController(ISender sender, IEnrollmentImportReader reader) : ControllerBase
{
    private Guid AdminId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ClassRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpGet]
    public Task<PagedResult<ClassEnrollmentDto>> List(Guid classId, [FromQuery] EnrollmentStatus? status,
        [FromQuery] Guid? studentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        sender.Send(new GetClassEnrollmentsQuery(classId, status, studentId, page, pageSize), ct);

    [HttpPost]
    public async Task<ActionResult<ClassEnrollmentDto>> Enroll(Guid classId, EnrollStudentCommand command, CancellationToken ct)
    {
        command.ClassId = classId;
        command.AdminId = AdminId;
        var result = await sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("bulk")]
    public Task<BulkEnrollStudentsResult> Bulk(Guid classId, BulkEnrollStudentsCommand command, CancellationToken ct)
    {
        command.ClassId = classId;
        command.AdminId = AdminId;
        return sender.Send(command, ct);
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024 + 64 * 1024)]
    public async Task<ImportStudentsResult> Import(
        Guid classId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length is <= 0 or > 2 * 1024 * 1024)
            throw new ClassRuleException(
                "invalid_import_file",
                "File must be non-empty and at most 2 MB.");

        await using var stream = file.OpenReadStream();

        var rows = await reader.ReadAsync(
            stream,
            file.FileName,
            ct);

        return await sender.Send(
            new ImportStudentsCommand(classId, AdminId, rows),
            ct);
    }

    [HttpPost("{enrollmentId:guid}/withdraw")]
    public Task<ClassEnrollmentDto> Withdraw(Guid classId, Guid enrollmentId, CancellationToken ct) =>
        sender.Send(new WithdrawStudentCommand(classId, enrollmentId, AdminId), ct);
}
