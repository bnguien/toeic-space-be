using ToeicSpace.Content.Application.DTOs.VocabularyLookup;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services.Enrichment;

namespace ToeicSpace.Content.Application.Features.News.Queries.LookupNewsWord;

public sealed class LookupNewsWordHandler(WordEnricher enricher, EnrichmentSettings settings)
    : IRequestHandler<LookupNewsWordQuery, WordLookupDto>
{
    public async Task<WordLookupDto> Handle(LookupNewsWordQuery request, CancellationToken ct)
    {
        var result = await enricher.EnrichAsync(new EnrichRequest(request.Text, request.Context),
            new EnrichOptions(AllowAi: false, Interactive: true, TimeSpan.FromSeconds(settings.LookupDeadlineSeconds)),
            ct);
        return result.ToLookupDto(existing: null);
    }
}
