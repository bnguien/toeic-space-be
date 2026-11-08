using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VideoWatches.Commands.SetVideoPublic;

/// <summary>
/// Sharing a video someone already shares is allowed; the response says so (<c>duplicate</c>: first sharer and how many)
/// so the learner understands why the shared card shows another name.
/// </summary>
public sealed class SetVideoPublicHandler(IVideoWatchRepository watches, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<SetVideoPublicCommand, SetVideoPublicResultDto>
{
    public async Task<SetVideoPublicResultDto> Handle(SetVideoPublicCommand request, CancellationToken ct)
    {
        var row = await watches.GetByIdForUpdateAsync(request.LearnerId, request.Id, ct)
            ?? throw VideoErrors.WatchedVideoNotFound();
        row.IsPublic = request.IsPublic;
        if (request.IsPublic)
        {
            var name = request.DisplayName?.Trim();
            if (!string.IsNullOrEmpty(name)) row.SharerName = name.Length <= VideoWatch.SharerNameMaxLength ? name : name[..VideoWatch.SharerNameMaxLength];
            var avatar = request.AvatarUrl?.Trim();
            if (avatar is { Length: > 0 and <= VideoWatch.SharerAvatarMaxLength }
                && (avatar.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || avatar.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
                row.SharerAvatar = avatar;
        }
        row.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(ct);

        DuplicateShareDto? duplicate = null;
        if (row.IsPublic)
        {
            var others = await watches.GetOtherSharersAsync(request.LearnerId, row.VideoId, ct);
            if (others.Count > 0)
                duplicate = new DuplicateShareDto(others.Count + 1, others[0].ToDto(mine: false).UserName);
        }
        return new SetVideoPublicResultDto(row.ToDto(mine: true), duplicate);
    }
}
