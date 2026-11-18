using ToeicSpace.Identity.Application.Models;

namespace ToeicSpace.Identity.Application.Features.Authentication.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<AuthenticatedUser>;
