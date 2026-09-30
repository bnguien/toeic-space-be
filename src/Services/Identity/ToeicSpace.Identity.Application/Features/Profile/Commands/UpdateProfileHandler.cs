using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Features.Profile.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.Profile.Commands;

public sealed class UpdateProfileHandler : 
    IRequestHandler<UpdateProfileCommand, UserProfileResult>
{
    private readonly IUserRepository _userRepository;

    public UpdateProfileHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserProfileResult> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            throw AppException.NotFound("User profile not found");
        }

        user.FullName = request.FullName;
        user.Phone = request.Phone;
        user.AvatarUrl = request.AvatarUrl;
        user.DateOfBirth = request.DateOfBirth;
        user.Gender = request.Gender;
        user.Biography = request.Biography;
        user.TargetScore = request.TargetScore;
        user.CurrentLevel = request.CurrentLevel;

        await _userRepository.SaveChangesAsync(cancellationToken);
        return user.ToUserProfileResults();
    }
}