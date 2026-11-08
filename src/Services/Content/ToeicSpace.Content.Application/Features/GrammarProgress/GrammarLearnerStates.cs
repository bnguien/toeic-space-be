using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.GrammarProgress;

internal static class GrammarLearnerStates
{
    /// <summary>The learner's state row, tracked; created (and added) when missing.</summary>
    public static async Task<GrammarLearnerState> GetOrCreateAsync(IGrammarLearningRepository repository, Guid learnerId,
        CancellationToken ct)
    {
        var state = await repository.GetStateAsync(learnerId, true, ct);
        if (state is not null) return state;
        state = new GrammarLearnerState { LearnerId = learnerId };
        repository.AddState(state);
        return state;
    }
}
