namespace ToeicSpace.Assessment.Application.DTOs.Tests;

public sealed record TestPartSummaryDto(
    ToeicPart Part,
    int QuestionCount,
    int ExpectedQuestionCount);
