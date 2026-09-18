using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSetStatus;

public sealed class UpdatePracticeSetStatusHandler : IRequestHandler<UpdatePracticeSetStatusCommand, PracticeSetDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ContentCacheInvalidator _cacheInvalidator;

    public UpdatePracticeSetStatusHandler(
        IAssessmentDbContext context,
        ContentCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PracticeSetDetailDto> Handle(
        UpdatePracticeSetStatusCommand request,
        CancellationToken cancellationToken)
    {
        var practiceSet = await _context.ToeicPracticeSets
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

        if (request.Status == ContentStatus.Active && practiceSet.Status != ContentStatus.Active)
        {
            var questions = await _context.ToeicPracticeSetItems
                .AsNoTracking()
                .Where(item => item.PracticeSetId == practiceSet.Id)
                .Select(item => new { item.Question.Part, item.Question.Status })
                .ToListAsync(cancellationToken);

            if (questions.Count == 0)
            {
                throw AppException.Conflict(
                    "The practice set cannot be published because it has no questions.",
                    ErrorCodes.PracticeSetNotPublishable);
            }

            if (questions.Any(question => question.Part != practiceSet.Part || question.Status != ContentStatus.Active))
            {
                throw AppException.Conflict(
                    $"The practice set cannot be published: every question must be an Active Part {(int)practiceSet.Part} question.",
                    ErrorCodes.PracticeSetNotPublishable);
            }
        }

        practiceSet.Status = request.Status;
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidatePracticeSetsAsync([practiceSet.Id], cancellationToken);

        var items = await _context.LoadItemsAsync(practiceSet.Id, cancellationToken);

        return practiceSet.ToDetailDto(items);
    }
}
