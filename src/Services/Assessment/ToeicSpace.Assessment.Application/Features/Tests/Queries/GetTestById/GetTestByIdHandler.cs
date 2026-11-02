using ToeicSpace.Assessment.Application.Common.Querying;
using ToeicSpace.Assessment.Application.DTOs.Tests;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Mappings;

namespace ToeicSpace.Assessment.Application.Features.Tests.Queries.GetTestById;

public sealed class GetTestByIdHandler : IRequestHandler<GetTestByIdQuery, TestDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetTestByIdHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TestDetailDto> Handle(
        GetTestByIdQuery request,
        CancellationToken cancellationToken)
    {
        var test = await _context.ToeicTests
            .AsNoTracking()
            .VisibleTo(_currentUser.CanManageContent)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicTest), request.Id);

        var (passageCount, questionCounts) = await _context.LoadStatisticsAsync(test.Id, cancellationToken);

        return test.ToDetailDto(passageCount, questionCounts);
    }
}
