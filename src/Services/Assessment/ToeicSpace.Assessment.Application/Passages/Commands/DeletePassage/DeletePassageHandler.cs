using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;

namespace ToeicSpace.Assessment.Application.Passages.Commands.DeletePassage;

public sealed class DeletePassageHandler : IRequestHandler<DeletePassageCommand>
{
    private readonly IAssessmentDbContext _context;
    private readonly ContentCacheInvalidator _cacheInvalidator;
    private readonly TimeProvider _timeProvider;

    public DeletePassageHandler(
        IAssessmentDbContext context,
        ContentCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
        _timeProvider = timeProvider;
    }

    public async Task Handle(
        DeletePassageCommand request,
        CancellationToken cancellationToken)
    {
        var passage = await _context.ToeicPassages
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicPassage), request.Id);

        var hasQuestions = await _context.ToeicQuestions
            .AnyAsync(question => question.PassageId == passage.Id, cancellationToken);

        if (hasQuestions)
        {
            throw AppException.Conflict(
                "Cannot delete a passage that still has questions.",
                ErrorCodes.PassageInUse);
        }

        passage.MarkAsDeleted(_timeProvider.GetUtcNow().UtcDateTime);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidateTestsAsync([passage.TestId], cancellationToken);
    }
}
