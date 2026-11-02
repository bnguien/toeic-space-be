namespace ToeicSpace.Assessment.Application.DTOs.Bank;

public sealed record BankStatusStatsDto(
    ContentStatus Status,
    int Questions);
