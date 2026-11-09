using ToeicSpace.Assessment.Application.Common.Querying;
using ToeicSpace.Assessment.Application.DTOs.PracticeSets;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Mappings;

namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Queries.GetPracticeSetById;

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

        var result = practiceSet.ToDetailDto(items);
        return _currentUser.CanManageContent ? result : result with { CreatedByUserId = null };
    }
}
