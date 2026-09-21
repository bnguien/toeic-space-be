namespace ToeicSpace.Assessment.Application.Dtos;

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

/// <param name="QuestionsPerTest">How many questions of this part a standard TOEIC test has.</param>
public sealed record BankPartStatsDto(
    ToeicPart Part,
    ToeicSection Section,
    int Questions,
    int TestQuestions,
    int BankQuestions,
    int Passages,
    int QuestionsPerTest);

public sealed record BankStatusStatsDto(
    ContentStatus Status,
    int Questions);

public sealed record BankDifficultyStatsDto(
    QuestionDifficulty DifficultyLevel,
    int Questions);

public sealed record BankPracticeSetStatsDto(
    PracticeSetKind Kind,
    int Sets,
    int Questions);
