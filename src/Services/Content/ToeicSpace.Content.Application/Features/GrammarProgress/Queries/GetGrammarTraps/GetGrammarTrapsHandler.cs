using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarTraps;

public sealed class GetGrammarTrapsHandler(GrammarContentProvider content, IGrammarLearningRepository repository)
    : IRequestHandler<GetGrammarTrapsQuery, IReadOnlyList<TrapDto>>
{
    public async Task<IReadOnlyList<TrapDto>> Handle(GetGrammarTrapsQuery request, CancellationToken ct)
    {
        var bundle = await content.GetAsync(ct);
        var rows = await repository.ListTrapsAsync(request.LearnerId, GetGrammarTrapsQuery.Top, ct);
        return rows.Select(r => new TrapDto(r.TrapCode, bundle.TrapDescriptions.GetValueOrDefault(r.TrapCode, string.Empty),
            r.TrapCode.Split('#', 2)[0], r.WrongCount, r.QuestionCount)).ToList();
    }
}
