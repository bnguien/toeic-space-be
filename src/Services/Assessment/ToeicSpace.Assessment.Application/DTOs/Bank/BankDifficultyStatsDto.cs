namespace ToeicSpace.Assessment.Application.DTOs.Bank;

public sealed record BankDifficultyStatsDto(
    QuestionDifficulty DifficultyLevel,
    int Questions);
