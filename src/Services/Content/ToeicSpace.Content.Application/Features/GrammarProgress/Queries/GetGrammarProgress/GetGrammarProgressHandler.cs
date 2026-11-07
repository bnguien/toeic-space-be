using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Services.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarProgress;

public sealed class GetGrammarProgressHandler(GrammarContentProvider content, GrammarProgressService progress)
    : IRequestHandler<GetGrammarProgressQuery, GrammarProgressDto>
{
    public async Task<GrammarProgressDto> Handle(GetGrammarProgressQuery request, CancellationToken ct) =>
        await progress.ProgressAsync(request.LearnerId, await content.GetAsync(ct), ct);
}
