using ToeicSpace.Identity.Application.Features.UserManagement.Results;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<ManagedUserDetailResult>;
