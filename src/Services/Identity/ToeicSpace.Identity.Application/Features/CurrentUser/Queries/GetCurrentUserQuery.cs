using ToeicSpace.Identity.Application.Models;

namespace ToeicSpace.Identity.Application.Features.CurrentUser.Queries;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<AuthenticatedUser>;
