using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetSetDetail;

public sealed record GetSetDetailQuery(Guid SetId) : IRequest<SetDetailDto>;
