using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.DTOs.VocabularyPreferences;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.VocabularyPreferences.Commands.UpdatePreferences;

/// <summary>
/// Partial update: only the properties present in the body change; ids accept null to clear. <see cref="Study"/> is
/// merged field by field into the stored study settings.
/// </summary>
public sealed record UpdatePreferencesCommand(Optional<double> FontScale = default,
    Optional<bool> SidebarCollapsed = default, Optional<bool> Snowfall = default, Optional<bool> AutoPlayAudio = default,
    Optional<StudyMode> DefaultMode = default, Optional<int> DailyGoal = default, Optional<Guid?> LastStageId = default,
    Optional<VocabularyView> LastView = default, Optional<Guid?> LastFolderId = default,
    Optional<Guid?> LastFolderPickerId = default, [property: JsonIgnore] Guid LearnerId = default,
    Optional<int?> SidebarWidth = default, StudySettingsInput? Study = null)
    : IRequest<PreferencesDto>;
