using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Practice.Common;
using ToeicSpace.BuildingBlocks.Pagination;

namespace ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSets;

public sealed class GetPracticeSetsHandler : IRequestHandler<GetPracticeSetsQuery, PaginatedResult<PracticeSetDto>>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetPracticeSetsHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<PracticeSetDto>> Handle(
        GetPracticeSetsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ToeicPracticeSets
            .AsNoTracking()
            .VisibleTo(_currentUser.CanManageContent);

        if (_currentUser.CanManageContent && request.Status.HasValue)
        {
            query = query.Where(practiceSet => practiceSet.Status == request.Status);
        }

        if (request.Part.HasValue)
        {
            query = query.Where(practiceSet => practiceSet.Part == request.Part);
        }

        if (request.Kind.HasValue)
        {
            query = query.Where(practiceSet => practiceSet.Kind == request.Kind);
        }

        if (request.Level.HasValue)
        {
            query = query.Where(practiceSet => practiceSet.Level == request.Level);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(practiceSet => practiceSet.Title.Contains(search) || practiceSet.Code.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(practiceSet => practiceSet.Part)
            .ThenBy(practiceSet => practiceSet.Kind)
            .ThenBy(practiceSet => practiceSet.OrderIndex)
            .ThenBy(practiceSet => practiceSet.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(PracticeSetMappingExtensions.DtoProjection)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<PracticeSetDto>(items, totalCount, request.Page, request.PageSize);
    }
}
