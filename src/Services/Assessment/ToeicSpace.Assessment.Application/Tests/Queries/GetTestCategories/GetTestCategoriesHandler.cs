using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Tests.Common;

namespace ToeicSpace.Assessment.Application.Tests.Queries.GetTestCategories;

public sealed class GetTestCategoriesHandler : IRequestHandler<GetTestCategoriesQuery, IReadOnlyList<TestCategorySummaryDto>>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetTestCategoriesHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TestCategorySummaryDto>> Handle(
        GetTestCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var tests = await _context.ToeicTests
            .AsNoTracking()
            .VisibleTo(_currentUser.CanManageContent)
            .Where(test => test.Category != null)
            .Select(test => new { Category = test.Category!, test.Year })
            .ToListAsync(cancellationToken);

        return tests
            .GroupBy(test => test.Category)
            .Select(group => new TestCategorySummaryDto(
                group.Key,
                group.Count(),
                group
                    .Where(test => test.Year.HasValue)
                    .Select(test => test.Year!.Value)
                    .Distinct()
                    .OrderDescending()
                    .ToList()))
            .OrderByDescending(category => category.TestCount)
            .ThenBy(category => category.Category)
            .ToList();
    }
}
