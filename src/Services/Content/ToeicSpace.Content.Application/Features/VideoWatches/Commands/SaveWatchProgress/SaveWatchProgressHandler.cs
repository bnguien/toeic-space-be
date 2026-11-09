using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoWatches.Commands.SaveWatchProgress;

public sealed class SaveWatchProgressHandler(IVideoWatchRepository watches, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<SaveWatchProgressCommand, SaveProgressResultDto>
{
    public async Task<SaveProgressResultDto> Handle(SaveWatchProgressCommand request, CancellationToken ct)
    {
        // The row is created by resolve, before a single second is watched: no row = never opened.
        var row = await watches.GetForUpdateAsync(request.LearnerId, request.VideoId, ct)
            ?? throw VideoErrors.WatchedVideoNotFound();
        var now = clock.GetUtcNow().UtcDateTime;
        WatchProgressRules.Apply(row, request.Position, request.Duration, now);
        row.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(ct);
        return row.ToSaveProgressDto();
    }
}
