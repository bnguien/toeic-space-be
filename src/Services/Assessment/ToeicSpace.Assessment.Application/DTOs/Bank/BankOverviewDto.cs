namespace ToeicSpace.Assessment.Application.DTOs.Bank;

/// <summary>Counts behind the question bank overview of the admin CMS.</summary>
public sealed record BankOverviewDto(
    int TotalQuestions,
    int ListeningQuestions,
    int ReadingQuestions,
    int TestQuestions,
    int BankQuestions,
    int TotalTests,
    int ActiveTests,
    int TotalPracticeSets,
    int TotalPassages,
    IReadOnlyList<BankPartStatsDto> Parts,
    IReadOnlyList<BankStatusStatsDto> Statuses,
    IReadOnlyList<BankDifficultyStatsDto> Difficulties,
    IReadOnlyList<BankPracticeSetStatsDto> PracticeSets);
