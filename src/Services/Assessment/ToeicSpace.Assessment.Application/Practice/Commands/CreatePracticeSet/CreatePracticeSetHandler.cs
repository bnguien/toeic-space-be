using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Commands.CreatePracticeSet;

public sealed class CreatePracticeSetHandler : IRequestHandler<CreatePracticeSetCommand, PracticeSetDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreatePracticeSetHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PracticeSetDetailDto> Handle(
        CreatePracticeSetCommand request,
        CancellationToken cancellationToken)
    {
        await _context.EnsureCodeIsAvailableAsync(request.Code.Trim(), currentPracticeSetId: null, cancellationToken);

        var practiceSet = new ToeicPracticeSet
        {
            Id = Guid.NewGuid(),
            Status = ContentStatus.Draft,
            CreatedByUserId = _currentUser.UserId
        };

        PracticeSetContentValidator.ApplyContent(practiceSet, request);

        _context.ToeicPracticeSets.Add(practiceSet);
        await _context.SaveChangesAsync(cancellationToken);

        return practiceSet.ToDetailDto([]);
    }
}
