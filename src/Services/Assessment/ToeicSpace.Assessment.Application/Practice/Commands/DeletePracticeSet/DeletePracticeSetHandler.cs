using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Commands.DeletePracticeSet;

public sealed class DeletePracticeSetHandler : IRequestHandler<DeletePracticeSetCommand>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ContentCacheInvalidator _cacheInvalidator;
    private readonly TimeProvider _timeProvider;

    public DeletePracticeSetHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser,
        ContentCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _cacheInvalidator = cacheInvalidator;
        _timeProvider = timeProvider;
    }

    public async Task Handle(
        DeletePracticeSetCommand request,
        CancellationToken cancellationToken)
    {
        await _context.ExecutePracticeSetUpdateAsync(request.Id, async () =>
        {
            var practiceSet = await _context.ToeicPracticeSets
                .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
                ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

            PracticeSetAuthorization.EnsureCanManage(practiceSet, _currentUser);

            practiceSet.MarkAsDeleted(_timeProvider.GetUtcNow().UtcDateTime);
            await _context.SaveChangesAsync(cancellationToken);

            return practiceSet.Id;
        }, cancellationToken);

        await _cacheInvalidator.InvalidatePracticeSetsAsync([request.Id], cancellationToken);
    }
}
