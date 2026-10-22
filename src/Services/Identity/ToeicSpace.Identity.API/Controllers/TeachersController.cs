using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Identity.Application.Features.Teachers.Queries;
using ToeicSpace.Identity.Application.Features.Teachers.Results;

namespace ToeicSpace.Identity.API.Controllers;

[ApiController]
public sealed class TeachersController(ISender sender) : ControllerBase
{
    [HttpGet("api/v1/users/teachers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GetTeachersResult>> Search([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await sender.Send(new GetTeachersQuery(search, page, pageSize), ct));
    }
}
