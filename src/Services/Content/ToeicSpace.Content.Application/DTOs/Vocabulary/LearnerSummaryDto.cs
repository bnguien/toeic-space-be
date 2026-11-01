using ToeicSpace.Content.Application.DTOs.StudySessions;

namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

/// <summary>
/// <paramref name="NextSet"/> is the first lesson in roadmap order with at least one word not started yet;
/// <paramref name="ActiveSession"/> the most recently used active study session; <paramref name="DailyGoal"/> comes
/// from the learner's preferences (compare with <paramref name="ReviewedToday"/>).
/// </summary>
public sealed record LearnerSummaryDto(int Learned, int Learning, int Mastered, int Due, int StreakDays,
    int ReviewedToday, int LessonCount, int LessonsCompleted, int LibraryWordCount, NextSetDto? NextSet,
    IReadOnlyList<StageProgressDto> Stages, ActiveSessionDto? ActiveSession = null,
    int DailyGoal = ToeicSpace.Content.Domain.Entities.LearnerVocabularyPreferences.DefaultDailyGoal);
