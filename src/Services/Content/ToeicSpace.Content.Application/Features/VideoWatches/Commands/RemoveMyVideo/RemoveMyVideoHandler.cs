using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VideoWatches.Commands.RemoveMyVideo;

public sealed class RemoveMyVideoHandler(IVideoWatchRepository watches, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<RemoveMyVideoCommand>
{
    public async Task Handle(RemoveMyVideoCommand request, CancellationToken ct)
    {
        var row = await watches.GetByIdForUpdateAsync(request.LearnerId, request.Id, ct)
            ?? throw VideoErrors.WatchedVideoNotFound();
        row.IsDeleted = true;
        row.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(ct);
    }
}
