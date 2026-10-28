using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Identity.Application.Features.Students.Queries;
using ToeicSpace.Identity.Application.Features.Students.Results;

namespace ToeicSpace.Identity.API.Controllers;

[ApiController]
public sealed class StudentsController(ISender sender) : ControllerBase
{
    [HttpGet("api/v1/users/students")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GetStudentsResult>> Search([FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetStudentsQuery(search, page, pageSize), ct));
}
