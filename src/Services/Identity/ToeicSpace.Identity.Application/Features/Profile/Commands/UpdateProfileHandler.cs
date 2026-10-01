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
        
        if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName;
        
        if (request.Phone != null) user.Phone = request.Phone;
        if (request.AvatarUrl != null) user.AvatarUrl = request.AvatarUrl;
        if (request.Biography != null) user.Biography = request.Biography;
        
        if (request.DateOfBirth.HasValue) user.DateOfBirth = request.DateOfBirth;
        if (request.Gender.HasValue) user.Gender = request.Gender;
        if (request.TargetScore.HasValue) user.TargetScore = request.TargetScore;
        if (request.CurrentLevel.HasValue) user.CurrentLevel = request.CurrentLevel;

        await _userRepository.SaveChangesAsync(cancellationToken);
        return user.ToUserProfileResults();
    }
}