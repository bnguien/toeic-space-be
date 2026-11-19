using ToeicSpace.Assessment.Application.DTOs.Tests;


namespace ToeicSpace.Assessment.Application.Features.Tests.Queries.GetTestCategories;

public sealed record GetTestCategoriesQuery : IRequest<IReadOnlyList<TestCategorySummaryDto>>;
