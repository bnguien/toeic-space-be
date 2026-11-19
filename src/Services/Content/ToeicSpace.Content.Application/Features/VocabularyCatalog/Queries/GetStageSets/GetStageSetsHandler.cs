using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;

public sealed class GetStageSetsHandler(IVocabularyCatalogRepository catalog)
    : IRequestHandler<GetStageSetsQuery, IReadOnlyList<SetSummaryDto>>
{
    public async Task<IReadOnlyList<SetSummaryDto>> Handle(GetStageSetsQuery request, CancellationToken ct)
    {
        if (!await catalog.StageExistsAsync(request.StageId, ct)) throw VocabularyErrors.StageNotFound();
        return (await catalog.GetSetsWithWordsAsync(request.StageId, ct)).Select(x => x.ToSummaryDto()).ToArray();
    }
}
