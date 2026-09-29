using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Features.Profile.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.Profile.Queries;

public sealed class GetProfileHandler : IRequestHandler<GetProfileQuery, UserProfileResult>
{
    private readonly IUserRepository _userRepository;

    public GetProfileHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }
    public async Task<UserProfileResult> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            throw AppException.NotFound("User profile not found.");
        }

        return user.ToUserProfileResults();
    }
}