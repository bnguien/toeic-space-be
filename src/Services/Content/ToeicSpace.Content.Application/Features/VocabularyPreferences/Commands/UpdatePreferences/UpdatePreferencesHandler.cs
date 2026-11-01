using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.DTOs.VocabularyPreferences;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VocabularyPreferences.Commands.UpdatePreferences;

public sealed class UpdatePreferencesHandler(IVocabularyPreferencesRepository preferences, IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<UpdatePreferencesCommand, PreferencesDto>
{
    public async Task<PreferencesDto> Handle(UpdatePreferencesCommand request, CancellationToken ct)
    {
        var row = await preferences.GetForUpdateAsync(request.LearnerId, ct);
        if (row is null)
        {
            row = new LearnerVocabularyPreferences { LearnerId = request.LearnerId };
            preferences.Add(row);
        }
        Set(request.FontScale, v => row.FontScale = v);
        Set(request.SidebarCollapsed, v => row.SidebarCollapsed = v);
        Set(request.SidebarWidth, v => row.SidebarWidth = v);
        Set(request.Snowfall, v => row.Snowfall = v);
        Set(request.AutoPlayAudio, v => row.AutoPlayAudio = v);
        Set(request.DefaultMode, v => row.DefaultMode = v);
        Set(request.DailyGoal, v => row.DailyGoal = v);
        Set(request.LastStageId, v => row.LastStageId = v);
        Set(request.LastView, v => row.LastView = v);
        Set(request.LastFolderId, v => row.LastFolderId = v);
        Set(request.LastFolderPickerId, v => row.LastFolderPickerId = v);
        row.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(ct);
        return row.ToDto();
    }

    private static void Set<T>(Optional<T> value, Action<T> assign)
    {
        if (value.IsSet) assign(value.Value!);
    }
}
