namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

/// <summary>Days until the next review per grade; 0 means the current schedule is kept.</summary>
public sealed record SchedulePreviewDto(int Forget, int Hard, int Good, int Easy);
