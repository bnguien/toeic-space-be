using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ToeicSpace.BuildingBlocks.Pagination;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Identity.API.Contracts;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserRole;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserStatus;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.CreateUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.DeleteUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.RestoreUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.RevokeUserSessions;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.UpdateUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUserById;
using ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUsers;
using ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUserStatistics;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.API.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Produces("application/json")]
[Authorize(Roles = AdministratorRole)]
public sealed class AdminUsersController : ControllerBase
{
    private const string AdministratorRole = "Admin";

    private readonly IMediator _mediator;

    public AdminUsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "Search users",
        Description = "Filters by name, email or phone, role and status. Deleted=true lists soft-deleted accounts.")]
    [ProducesResponseType(typeof(PaginatedResult<ManagedUserResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] UserListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetUsersQuery(
                request.Search,
                request.Role,
                request.Status,
                request.Deleted,
                request.Sort,
                request.Page,
                request.PageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("stats")]
    [SwaggerOperation(
        Summary = "Count users",
        Description = "Counts by status for one role, or for every account when no role is given. " +
            "period (24h, 3d, 7d, 30d, 90d) picks the trend range; tzOffset (minutes east of UTC, 420 for Vietnam) aligns its buckets.")]
    [ProducesResponseType(typeof(UserStatisticsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] string? role,
        [FromQuery] int tzOffset,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetUserStatisticsQuery(role, tzOffset, period), cancellationToken));

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(ManagedUserDetailResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(
        Guid userId,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetUserByIdQuery(userId), cancellationToken));

    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a user",
        Description = "Creates an active, verified account. The temporary password is only returned in this response.")]
    [ProducesResponseType(typeof(CreateUserResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CreateUserCommand(ActorId(), request.FullName, request.Email, request.Phone, request.Role),
            cancellationToken);

        return CreatedAtAction(nameof(GetUser), new { userId = result.User.Id }, result);
    }

    [HttpPut("{userId:guid}")]
    [SwaggerOperation(Summary = "Update the name and phone number")]
    [ProducesResponseType(typeof(ManagedUserDetailResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUser(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new UpdateUserCommand(ActorId(), userId, request.FullName, request.Phone),
            cancellationToken));

    [HttpPut("{userId:guid}/role")]
    [SwaggerOperation(
        Summary = "Change the role",
        Description = "Ends every session of the user. The last active administrator cannot be demoted.")]
    [ProducesResponseType(typeof(ManagedUserDetailResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeRole(
        Guid userId,
        [FromBody] ChangeUserRoleRequest request,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new ChangeUserRoleCommand(ActorId(), userId, request.Role),
            cancellationToken));

    [HttpPut("{userId:guid}/status")]
    [SwaggerOperation(
        Summary = "Suspend or reactivate",
        Description = "Status is Active or Suspended. Suspending ends every session of the user.")]
    [ProducesResponseType(typeof(ManagedUserDetailResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeStatus(
        Guid userId,
        [FromBody] ChangeUserStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new ChangeUserStatusCommand(ActorId(), userId, request.Status),
            cancellationToken));

    [HttpPost("{userId:guid}/sessions/revoke")]
    [SwaggerOperation(Summary = "Sign the user out on every device")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSessions(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new RevokeUserSessionsCommand(ActorId(), userId), cancellationToken);

        return NoContent();
    }

    [HttpDelete("{userId:guid}")]
    [SwaggerOperation(Summary = "Soft delete a user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteUserCommand(ActorId(), userId), cancellationToken);

        return NoContent();
    }

    [HttpPost("{userId:guid}/restore")]
    [SwaggerOperation(Summary = "Restore a soft-deleted user")]
    [ProducesResponseType(typeof(ManagedUserDetailResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> RestoreUser(
        Guid userId,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new RestoreUserCommand(ActorId(), userId), cancellationToken));

    private Guid ActorId()
        => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var actorId)
            ? actorId
            : throw AppException.Unauthenticated(code: ErrorCodes.TokenInvalid);
}
