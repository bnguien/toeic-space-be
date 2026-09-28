using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Commands.ReplacePracticeSetItems;

public sealed class ReplacePracticeSetItemsHandler : IRequestHandler<ReplacePracticeSetItemsCommand, PracticeSetDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ContentCacheInvalidator _cacheInvalidator;

    public ReplacePracticeSetItemsHandler(
        IAssessmentDbContext context,
        ContentCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PracticeSetDetailDto> Handle(
        ReplacePracticeSetItemsCommand request,
        CancellationToken cancellationToken)
    {
        var practiceSet = await _context.ToeicPracticeSets
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

        if (practiceSet.Status == ContentStatus.Active && request.QuestionIds.Count == 0)
        {
            throw AppException.Conflict(
                "A published practice set must keep at least one question.",
                ErrorCodes.PracticeSetNotPublishable);
        }

        var questions = await _context.ToeicQuestions
            .AsNoTracking()
            .Where(question => request.QuestionIds.Contains(question.Id))
            .Select(question => new { question.Id, question.Part, question.Status })
            .ToListAsync(cancellationToken);

        var missingIds = request.QuestionIds
            .Except(questions.Select(question => question.Id))
            .ToList();

        if (missingIds.Count > 0)
        {
            throw AppException.Validation(
                $"Questions not found: {string.Join(", ", missingIds.Take(10))}.",
                ErrorCodes.InvalidReference);
        }

        if (questions.Any(question => question.Part != practiceSet.Part))
        {
            throw AppException.Validation(
                $"Every question of this practice set must be a Part {(int)practiceSet.Part} question.",
                ErrorCodes.InvalidReference);
        }

        // Learners only see Active questions, so a published set may not take anything else.
        if (practiceSet.Status == ContentStatus.Active
            && questions.Any(question => question.Status != ContentStatus.Active))
        {
            throw AppException.Conflict(
                "A published practice set can only contain Active questions. "
                + "Publish those questions or move the set back to Draft first.",
                ErrorCodes.PracticeSetNotPublishable);
        }

        var existingItems = await _context.ToeicPracticeSetItems
            .Where(item => item.PracticeSetId == practiceSet.Id)
            .ToListAsync(cancellationToken);

        _context.ToeicPracticeSetItems.RemoveRange(existingItems);
        _context.ToeicPracticeSetItems.AddRange(request.QuestionIds.Select((questionId, index) => new ToeicPracticeSetItem
        {
            PracticeSetId = practiceSet.Id,
            QuestionId = questionId,
            OrderIndex = index
        }));

        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidatePracticeSetsAsync([practiceSet.Id], cancellationToken);

        var items = await _context.LoadItemsAsync(practiceSet.Id, cancellationToken);

        return practiceSet.ToDetailDto(items);
    }
}
