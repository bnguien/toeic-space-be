using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Exceptions;
using ToeicSpace.BuildingBlocks.Storage.Abstractions;

namespace ToeicSpace.Identity.Application.Features.Profile.Commands;

public sealed class UploadAvatarHandler : IRequestHandler<UploadAvatarCommand, string>
{
    private readonly IUserRepository _userRepository;
    private readonly IObjectStorageService _storageService;

    public UploadAvatarHandler(
        IUserRepository userRepository,
        IObjectStorageService storageService)
    {
        _userRepository = userRepository;
        _storageService = storageService;
    }

    public async Task<string> Handle(UploadAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) throw AppException.NotFound("User profile not found");

        var objectKey = $"avatars/user-{request.UserId}";

        var uploadUrl = await _storageService.GenerateUploadUrlAsync(
            objectKey, 
            request.ContentType, 
            TimeSpan.FromMinutes(15), 
            cancellationToken);

        user.AvatarUrl = $"{objectKey}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await _userRepository.SaveChangesAsync(cancellationToken);

        return uploadUrl;
    }
}