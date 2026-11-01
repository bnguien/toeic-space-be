using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Application.DTOs.VocabularyPreferences;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Mappings;

public static class StudySessionMappingExtensions
{
    public static StudySourceDto ToSourceDto(this StudySession x) => new(x.SourceKind, x.SourceId, x.SourceTitle);

    public static ActiveSessionDto ToActiveDto(this StudySession x) =>
        new(x.Id, x.Mode, x.ToSourceDto(), x.Position, x.Queue.Count);

    public static PreferencesDto ToDto(this LearnerVocabularyPreferences x) => new(x.FontScale, x.SidebarCollapsed,
        x.Snowfall, x.AutoPlayAudio, x.DefaultMode, x.DailyGoal, x.LastStageId, x.LastView, x.LastFolderId,
        x.LastFolderPickerId, x.UpdatedAt is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null, x.SidebarWidth);
}
