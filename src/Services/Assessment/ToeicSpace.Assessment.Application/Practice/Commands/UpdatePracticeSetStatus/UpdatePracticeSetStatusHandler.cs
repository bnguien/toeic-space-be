using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSetStatus;

public sealed class UpdatePracticeSetStatusHandler : IRequestHandler<UpdatePracticeSetStatusCommand, PracticeSetDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ContentCacheInvalidator _cacheInvalidator;

    public UpdatePracticeSetStatusHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser,
        ContentCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _currentUser = currentUser;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PracticeSetDetailDto> Handle(
        UpdatePracticeSetStatusCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _context.ExecutePracticeSetUpdateAsync(request.Id, async () =>
        {
            var practiceSet = await _context.ToeicPracticeSets
                .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
                ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

            PracticeSetAuthorization.EnsureCanManage(practiceSet, _currentUser);

            if (request.Status == ContentStatus.Active)
            {
                var questionIds = await _context.ToeicPracticeSetItems
                    .Where(item => item.PracticeSetId == practiceSet.Id)
                    .Select(item => item.QuestionId)
                    .ToListAsync(cancellationToken);
                await PracticeSetPublishingGuard.EnsureValidAsync(_context, practiceSet, questionIds, cancellationToken);
            }

            practiceSet.Status = request.Status;
            await _context.SaveChangesAsync(cancellationToken);

            var items = await _context.LoadItemsAsync(practiceSet.Id, cancellationToken);

            return practiceSet.ToDetailDto(items);
        }, cancellationToken);

        await _cacheInvalidator.InvalidatePracticeSetsAsync([request.Id], cancellationToken);

        return result;
    }
}
