namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

/// <summary>A lesson is completed when every one of its words has a progress row.</summary>
public sealed record StageProgressDto(Guid StageId, int WordCount, int Learning, int Mastered, int SetCount,
    int SetsCompleted);
