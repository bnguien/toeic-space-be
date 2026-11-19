using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetKnownWords;

public sealed class GetKnownWordsHandler(GrammarContentProvider content, IGrammarLearningRepository repository)
    : IRequestHandler<GetKnownWordsQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetKnownWordsQuery request, CancellationToken ct)
    {
        await content.GetTopicAsync(request.Topic, ct);
        var state = await repository.GetStateAsync(request.LearnerId, false, ct);
        return state?.KnownWords.GetValueOrDefault(request.Topic) ?? [];
    }
}
