using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;

public sealed record GetStageSetsQuery(Guid StageId) : IRequest<IReadOnlyList<SetSummaryDto>>;
