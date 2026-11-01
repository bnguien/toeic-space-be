using ToeicSpace.Content.Application.DTOs.VocabularyPreferences;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VocabularyPreferences.Queries.GetPreferences;

public sealed class GetPreferencesHandler(IVocabularyPreferencesRepository preferences)
    : IRequestHandler<GetPreferencesQuery, PreferencesDto>
{
    public async Task<PreferencesDto> Handle(GetPreferencesQuery request, CancellationToken ct) =>
        ((await preferences.GetAsync(request.LearnerId, ct))
         ?? new LearnerVocabularyPreferences { LearnerId = request.LearnerId }).ToDto();
}
