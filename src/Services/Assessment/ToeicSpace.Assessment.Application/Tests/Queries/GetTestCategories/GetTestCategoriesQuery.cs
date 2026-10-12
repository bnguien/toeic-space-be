using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Tests.Queries.GetTestCategories;

public sealed record GetTestCategoriesQuery : IRequest<IReadOnlyList<TestCategorySummaryDto>>;
