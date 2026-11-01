using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyEnrolledClassById;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyEnrolledClasses;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Route("api/v1/classes/me/enrolled")]
[Authorize(Roles = "User")]
public sealed class StudentClassesController(ISender sender) : ControllerBase
{
    private Guid StudentId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ClassRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpGet]
    public Task<PagedResult<ClassDto>> List([FromQuery] string? search, [FromQuery] ClassStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        sender.Send(new GetMyEnrolledClassesQuery(StudentId, search, status, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<ClassDetailDto> Get(Guid id, CancellationToken ct) =>
        sender.Send(new GetMyEnrolledClassByIdQuery(StudentId, id), ct);
}
