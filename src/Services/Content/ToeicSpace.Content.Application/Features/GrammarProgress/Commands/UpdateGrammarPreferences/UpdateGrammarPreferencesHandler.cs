using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.UpdateGrammarPreferences;

public sealed class UpdateGrammarPreferencesHandler(IGrammarLearningRepository repository, IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<UpdateGrammarPreferencesCommand, GrammarPreferencesDto>
{
    public async Task<GrammarPreferencesDto> Handle(UpdateGrammarPreferencesCommand request, CancellationToken ct)
    {
        var state = await GrammarLearnerStates.GetOrCreateAsync(repository, request.LearnerId, ct);
        state.DarkMode = request.DarkMode;
        state.RoadmapWidth = request.RoadmapWidth;
        state.RoadmapCollapsed = request.RoadmapCollapsed;
        state.Touch(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        return state.ToPreferencesDto();
    }
}
