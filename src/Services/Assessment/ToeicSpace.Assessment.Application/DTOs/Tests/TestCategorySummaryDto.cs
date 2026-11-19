namespace ToeicSpace.Assessment.Application.DTOs.Tests;

public sealed record TestCategorySummaryDto(
    string Category,
    int TestCount,
    IReadOnlyList<int> Years);
