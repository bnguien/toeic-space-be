using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Tests.Common;
using ToeicSpace.BuildingBlocks.Pagination;

namespace ToeicSpace.Assessment.Application.Tests.Queries.GetTests;

public sealed class GetTestsHandler : IRequestHandler<GetTestsQuery, PaginatedResult<TestDto>>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetTestsHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<TestDto>> Handle(
        GetTestsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ToeicTests
            .AsNoTracking()
            .VisibleTo(_currentUser.CanManageContent);

        if (_currentUser.CanManageContent && request.Status.HasValue)
        {
            query = query.Where(test => test.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim();
            query = query.Where(test => test.Category == category);
        }

        if (request.Year.HasValue)
        {
            query = query.Where(test => test.Year == request.Year);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Test columns are short and use a case-insensitive collation, so LIKE is enough here.
            var search = request.Search.Trim();
            query = query.Where(test =>
                test.Title.Contains(search)
                || test.Code.Contains(search)
                || (test.Category != null && test.Category.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(test => test.Category)
            .ThenBy(test => test.Code)
            .ThenBy(test => test.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(TestMappingExtensions.DtoProjection)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<TestDto>(items, totalCount, request.Page, request.PageSize);
    }
}
