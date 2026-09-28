using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;

namespace ToeicSpace.Assessment.Application.Practice.Commands.DeletePracticeSet;

public sealed class DeletePracticeSetHandler : IRequestHandler<DeletePracticeSetCommand>
{
    private readonly IAssessmentDbContext _context;
    private readonly ContentCacheInvalidator _cacheInvalidator;
    private readonly TimeProvider _timeProvider;

    public DeletePracticeSetHandler(
        IAssessmentDbContext context,
        ContentCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
        _timeProvider = timeProvider;
    }

    public async Task Handle(
        DeletePracticeSetCommand request,
        CancellationToken cancellationToken)
    {
        var practiceSet = await _context.ToeicPracticeSets
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

        practiceSet.MarkAsDeleted(_timeProvider.GetUtcNow().UtcDateTime);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidatePracticeSetsAsync([practiceSet.Id], cancellationToken);
    }
}
