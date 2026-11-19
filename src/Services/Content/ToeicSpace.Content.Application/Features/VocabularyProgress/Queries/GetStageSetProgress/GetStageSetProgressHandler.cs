using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStageSetProgress;

public sealed class GetStageSetProgressHandler(ILearnerProgressRepository progress,
    IVocabularyCatalogRepository catalog, TimeProvider clock)
    : IRequestHandler<GetStageSetProgressQuery, IReadOnlyList<SetProgressDto>>
{
    public async Task<IReadOnlyList<SetProgressDto>> Handle(GetStageSetProgressQuery request, CancellationToken ct)
    {
        if (!await catalog.StageExistsAsync(request.StageId, ct)) throw VocabularyErrors.StageNotFound();
        var today = SpacedRepetitionScheduler.LocalDay(clock.GetUtcNow().UtcDateTime, request.TzOffset);
        var membership = await catalog.GetMembershipAsync(request.StageId, null, ct);
        var rows = (await progress.GetForWordsAsync(request.LearnerId,
            membership.Select(x => x.WordId).Distinct().ToArray(), ct)).ToDictionary(x => x.WordId);
        return membership.GroupBy(x => (x.SetId, x.SetOrder))
            .OrderBy(x => x.Key.SetOrder)
            .Select(set =>
            {
                var known = set.Where(x => rows.ContainsKey(x.WordId)).Select(x => rows[x.WordId]).ToArray();
                var mastered = known.Count(x => x.IntervalDays >= SpacedRepetitionScheduler.MasteredIntervalDays);
                return new SetProgressDto(set.Key.SetId, set.Count(), known.Length - mastered, mastered,
                    known.Count(x => SpacedRepetitionScheduler.IsDue(x.DueOn, today)));
            })
            .Where(x => x.Learning + x.Mastered > 0)
            .ToArray();
    }
}
