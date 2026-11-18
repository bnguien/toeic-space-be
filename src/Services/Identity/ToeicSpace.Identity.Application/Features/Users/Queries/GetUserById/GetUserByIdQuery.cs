using ToeicSpace.Identity.Application.DTOs.Users;


namespace ToeicSpace.Identity.Application.Features.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<ManagedUserDetailResult>;
