using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>One row per learner; absent row = defaults.</summary>
public sealed class LearnerVocabularyPreferences
{
    public static readonly double[] FontScales = [0.875, 1, 1.125, 1.25];
    public const int MinDailyGoal = 5;
    public const int MaxDailyGoal = 200;
    public const int DefaultDailyGoal = 20;
    public const int MinSidebarWidth = 220;
    public const int MaxSidebarWidth = 420;

    public Guid LearnerId { get; set; }
    public double FontScale { get; set; } = 1;
    public bool SidebarCollapsed { get; set; }
    /// <summary>Sidebar width in pixels (220–420); null = layout default.</summary>
    public int? SidebarWidth { get; set; }
    public bool Snowfall { get; set; } = true;
    public bool AutoPlayAudio { get; set; } = true;
    public StudyMode DefaultMode { get; set; } = StudyMode.Flashcard;
    public int DailyGoal { get; set; } = DefaultDailyGoal;
    public Guid? LastStageId { get; set; }
    public VocabularyView LastView { get; set; } = VocabularyView.Roadmap;
    public Guid? LastFolderId { get; set; }
    /// <summary>Folder remembered by the "add word" dialog.</summary>
    public Guid? LastFolderPickerId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
