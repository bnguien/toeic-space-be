using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.BuildingBlocks.Pagination;

namespace ToeicSpace.Assessment.Application.Passages.Queries.GetPassages;

public sealed class GetPassagesHandler : IRequestHandler<GetPassagesQuery, PaginatedResult<PassageDto>>
{
    private readonly IAssessmentDbContext _context;

    public GetPassagesHandler(IAssessmentDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<PassageDto>> Handle(
        GetPassagesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ToeicPassages.AsNoTracking();

        if (request.TestId.HasValue)
        {
            query = query.Where(passage => passage.TestId == request.TestId);
        }

        if (request.BankOnly.HasValue)
        {
            query = request.BankOnly.Value
                ? query.Where(passage => passage.TestId == null)
                : query.Where(passage => passage.TestId != null);
        }

        if (request.Part.HasValue)
        {
            query = query.Where(passage => passage.Part == request.Part);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(passage => passage.Part)
            .ThenBy(passage => passage.OrderIndex)
            .ThenBy(passage => passage.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(PassageMappingExtensions.DtoProjection)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<PassageDto>(items, totalCount, request.Page, request.PageSize);
    }
}
