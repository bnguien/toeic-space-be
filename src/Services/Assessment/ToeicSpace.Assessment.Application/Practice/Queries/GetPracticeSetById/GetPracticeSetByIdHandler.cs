using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetById;

public sealed class GetPracticeSetByIdHandler : IRequestHandler<GetPracticeSetByIdQuery, PracticeSetDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetPracticeSetByIdHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PracticeSetDetailDto> Handle(
        GetPracticeSetByIdQuery request,
        CancellationToken cancellationToken)
    {
        var practiceSet = await _context.ToeicPracticeSets
            .AsNoTracking()
            .VisibleTo(_currentUser.CanManageContent)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

        var items = await _context.LoadItemsAsync(practiceSet.Id, cancellationToken);

        return practiceSet.ToDetailDto(items);
    }
}
