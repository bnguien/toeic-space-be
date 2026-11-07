using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Commands.SubmitReview;

public sealed class SubmitReviewHandler(ReviewRecorder recorder, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<SubmitReviewCommand, ReviewResultDto>
{
    public async Task<ReviewResultDto> Handle(SubmitReviewCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = SpacedRepetitionScheduler.LocalDay(now, request.TzOffset);
        var replay = await recorder.ReplayAsync(request.LearnerId, request.WordId, request.ClientEventId, today, ct);
        if (replay is not null) return replay;

        var result = await recorder.StageAsync(request.LearnerId, request.WordId, request.Mode, request.Grade,
            request.ClientEventId, now, today, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return result;
    }
}
