using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Centers.Queries.ListCenters;

public sealed record ListCentersQuery(string? Search, FacilityStatus? Status, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<CenterDto>>;
