using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetSetDetail;

/// <summary><see cref="LearnerId"/> comes from the access token when one is sent (Guid.Empty for guests).</summary>
public sealed record GetSetDetailQuery(Guid SetId, Guid LearnerId = default) : IRequest<SetDetailDto>;
