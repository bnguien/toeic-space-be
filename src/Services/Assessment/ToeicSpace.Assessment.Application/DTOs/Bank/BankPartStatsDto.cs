namespace ToeicSpace.Assessment.Application.DTOs.Bank;

/// <param name="QuestionsPerTest">How many questions of this part a standard TOEIC test has.</param>
public sealed record BankPartStatsDto(
    ToeicPart Part,
    ToeicSection Section,
    int Questions,
    int TestQuestions,
    int BankQuestions,
    int Passages,
    int QuestionsPerTest);
