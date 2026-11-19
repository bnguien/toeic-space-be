namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

/// <summary><paramref name="SavedWordId"/> is set when the word is in the learner's library.</summary>
public sealed record StudyItemDto(WordDto Word, ProgressDto Progress, SchedulePreviewDto Preview, Guid? SavedWordId);
