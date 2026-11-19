using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStages;

public sealed record GetStagesQuery : IRequest<IReadOnlyList<StageDto>>;
