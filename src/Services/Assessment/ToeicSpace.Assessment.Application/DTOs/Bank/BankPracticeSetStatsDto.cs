namespace ToeicSpace.Assessment.Application.DTOs.Bank;

public sealed record BankPracticeSetStatsDto(
    PracticeSetKind Kind,
    int Sets,
    int Questions);
