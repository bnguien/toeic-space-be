using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStages;

public sealed class GetStagesHandler(IVocabularyCatalogRepository catalog)
    : IRequestHandler<GetStagesQuery, IReadOnlyList<StageDto>>
{
    public async Task<IReadOnlyList<StageDto>> Handle(GetStagesQuery request, CancellationToken ct)
    {
        var stages = await catalog.GetStagesAsync(ct);
        var membership = (await catalog.GetMembershipAsync(null, null, ct)).ToLookup(x => x.StageId);
        return stages.Select(stage =>
        {
            var rows = membership[stage.Id].ToArray();
            return stage.ToDto(rows.Select(x => x.SetId).Distinct().Count(),
                rows.Select(x => x.WordId).Distinct().Count());
        }).ToArray();
    }
}
