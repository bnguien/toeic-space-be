using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Services;

/// <summary>Builds <see cref="StudySessionDto"/> (items with live progress, results and the completion summary).</summary>
public sealed class StudySessionPresenter(StudyQueueService queue, TimeProvider clock)
{
    public async Task<StudySessionDto> ToDtoAsync(StudySession x, int tzOffset, CancellationToken ct,
        ReviewResultDto? review = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = SpacedRepetitionScheduler.LocalDay(now, tzOffset);
        var items = await queue.BuildItemsAsync(x.LearnerId, x.Queue, today, ct);
        var status = x.EffectiveStatus(now);
        return new StudySessionDto(x.Id, x.Mode, x.ToSourceDto(), x.Position, x.Queue.Count, x.CorrectCount,
            x.ReviewCount, status, Utc(x.StartedAt), Utc(x.UpdatedAt), x.CompletedAt is { } done ? Utc(done) : null,
            items, x.Results.Select(r => new SessionResultDto(r.Key, r.Value)).ToArray(),
            status == StudySessionStatus.Completed ? Summarize(x) : null, review);
    }

    public static SessionSummaryDto Summarize(StudySession x)
    {
        var needReview = x.Results.Where(r => r.Value is not (ReviewGrade.Good or ReviewGrade.Easy))
            .Select(r => r.Key).ToArray();
        return new SessionSummaryDto(x.Results.Count - needReview.Length, needReview.Length, needReview);
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
