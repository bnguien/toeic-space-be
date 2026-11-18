using ToeicSpace.Identity.Application.DTOs.Profiles;


namespace ToeicSpace.Identity.Application.Features.Profiles.Queries.GetProfile;

public sealed record GetProfileQuery(Guid UserId) : IRequest<UserProfileResult>;