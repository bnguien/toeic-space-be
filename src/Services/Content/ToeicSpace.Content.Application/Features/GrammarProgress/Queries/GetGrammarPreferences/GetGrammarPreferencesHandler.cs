using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarPreferences;

public sealed class GetGrammarPreferencesHandler(IGrammarLearningRepository repository)
    : IRequestHandler<GetGrammarPreferencesQuery, GrammarPreferencesDto>
{
    public async Task<GrammarPreferencesDto> Handle(GetGrammarPreferencesQuery request, CancellationToken ct) =>
        (await repository.GetStateAsync(request.LearnerId, false, ct)).ToPreferencesDto();
}
