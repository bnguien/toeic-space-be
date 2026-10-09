using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Exceptions;
using ToeicSpace.BuildingBlocks.Storage.Abstractions;

namespace ToeicSpace.Identity.Application.Features.Profile.Commands;

public sealed class UploadAvatarHandler : IRequestHandler<UploadAvatarCommand, string>
{
    private readonly IObjectStorageService _storageService;

    public UploadAvatarHandler(
        IObjectStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<string> Handle(UploadAvatarCommand request, CancellationToken cancellationToken)
    {
        var objectKey = $"avatars/user-{request.UserId}";

        var uploadUrl = await _storageService.GenerateUploadUrlAsync(
            objectKey, 
            request.ContentType, 
            TimeSpan.FromMinutes(15), 
            cancellationToken);
        return uploadUrl;
    }
}