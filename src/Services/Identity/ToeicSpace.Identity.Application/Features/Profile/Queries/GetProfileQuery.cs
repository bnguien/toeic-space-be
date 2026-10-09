using ToeicSpace.Identity.Application.Features.Profile.Results;

namespace ToeicSpace.Identity.Application.Features.Profile.Queries;

public sealed record GetProfileQuery(Guid UserId) : IRequest<UserProfileResult>;