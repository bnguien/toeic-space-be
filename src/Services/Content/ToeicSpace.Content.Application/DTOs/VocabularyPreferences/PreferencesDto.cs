using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.VocabularyPreferences;

/// <summary><paramref name="Study"/> is always set (defaults when never saved).</summary>
public sealed record PreferencesDto(double FontScale, bool SidebarCollapsed, bool Snowfall, bool AutoPlayAudio,
    StudyMode DefaultMode, int DailyGoal, Guid? LastStageId, VocabularyView LastView, Guid? LastFolderId,
    Guid? LastFolderPickerId, DateTime? UpdatedAt, int? SidebarWidth = null, StudySettingsDto? Study = null);
