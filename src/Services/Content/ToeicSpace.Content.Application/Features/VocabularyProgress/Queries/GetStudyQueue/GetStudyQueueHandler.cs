using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStudyQueue;

/// <summary>
/// setId: every word of the lesson in order. folderId / library: every saved word, newest first. Limit applies
/// only to due=true (due date, then oldest review).
/// </summary>
public sealed class GetStudyQueueHandler(StudyQueueService queue, TimeProvider clock)
    : IRequestHandler<GetStudyQueueQuery, IReadOnlyList<StudyItemDto>>
{
    public async Task<IReadOnlyList<StudyItemDto>> Handle(GetStudyQueueQuery request, CancellationToken ct)
    {
        var today = SpacedRepetitionScheduler.LocalDay(clock.GetUtcNow().UtcDateTime, request.TzOffset);
        var (kind, sourceId) = StudyQueueService.KindOf(request.SetId, request.FolderId, request.Library, request.Due, null);
        var source = await queue.ResolveAsync(request.LearnerId, kind, sourceId, null, request.Limit, today, ct);
        return await queue.BuildItemsAsync(request.LearnerId, source.WordIds, today, ct);
    }
}
