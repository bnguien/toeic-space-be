using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationSummary;

/// <summary>Passed sentences, total active sentences and the average best accuracy of the learner.</summary>
public sealed class GetDictationSummaryHandler(IDictationCatalogRepository catalog,
    IDictationProgressRepository progressRepository) : IRequestHandler<GetDictationSummaryQuery, DictationSummaryDto>
{
    public async Task<DictationSummaryDto> Handle(GetDictationSummaryQuery request, CancellationToken ct)
    {
        var total = await catalog.CountActiveItemsAsync(ct);
        if (request.LearnerId is not { } learner) return new DictationSummaryDto(false, 0, total, 0);
        var rows = await progressRepository.GetProgressAsync(learner, null, ct);
        var accuracy = rows.Count == 0 ? 0 : (int)Math.Round(rows.Average(x => x.BestAccuracy), MidpointRounding.ToEven);
        return new DictationSummaryDto(true, rows.Count(x => x.Passed), total, accuracy);
    }
}
