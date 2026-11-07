using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Commands.ReplacePracticeSetItems;

public sealed class ReplacePracticeSetItemsHandler : IRequestHandler<ReplacePracticeSetItemsCommand, PracticeSetDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ContentCacheInvalidator _cacheInvalidator;

    public ReplacePracticeSetItemsHandler(
        IAssessmentDbContext context,
        ICurrentUserService currentUser,
        ContentCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _currentUser = currentUser;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PracticeSetDetailDto> Handle(
        ReplacePracticeSetItemsCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _context.ExecutePracticeSetUpdateAsync(request.Id, async () =>
        {
            var practiceSet = await _context.ToeicPracticeSets
                .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
                ?? throw AppException.NotFound(nameof(ToeicPracticeSet), request.Id);

            PracticeSetAuthorization.EnsureCanManage(practiceSet, _currentUser);

            var storedIds = await _context.ToeicPracticeSetItems.AsNoTracking()
                .Where(item => item.PracticeSetId == request.Id)
                .OrderBy(item => item.OrderIndex).Select(item => item.QuestionId).ToListAsync(cancellationToken);
            // Compare inside the set lock, before touching membership or order. Refetching in
            // the browser cannot close the race between two users submitting the same baseline.
            if (request.ExpectedQuestionIds is null || !storedIds.SequenceEqual(request.ExpectedQuestionIds))
            {
                throw AppException.Conflict(
                    "Practice set questions changed. Reload the question list before saving.",
                    ErrorCodes.ConcurrencyConflict);
            }
            await _context.LockPracticeContentAsync(storedIds.Concat(request.QuestionIds).Distinct().ToList(), cancellationToken);

            if (practiceSet.Status == ContentStatus.Active && request.QuestionIds.Count == 0)
            {
                throw AppException.Conflict(
                    "A published practice set must keep at least one question.",
                    ErrorCodes.PracticeSetNotPublishable);
            }

            var questions = await _context.ToeicQuestions
                .AsNoTracking()
                .Where(question => request.QuestionIds.Contains(question.Id))
                .Select(question => new { question.Id, question.Part })
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

            if (practiceSet.Status == ContentStatus.Active)
            {
                await PracticeSetPublishingGuard.EnsureValidAsync(_context, practiceSet, request.QuestionIds, cancellationToken, lockContent: false);
            }

            var existingItems = await _context.ToeicPracticeSetItems
                .Where(item => item.PracticeSetId == practiceSet.Id)
                .ToListAsync(cancellationToken);

            var requestedIds = request.QuestionIds.ToHashSet();
            _context.ToeicPracticeSetItems.RemoveRange(existingItems.Where(item => !requestedIds.Contains(item.QuestionId)));
            var retained = existingItems.ToDictionary(item => item.QuestionId);
            for (var index = 0; index < request.QuestionIds.Count; index++)
            {
                var questionId = request.QuestionIds[index];
                if (retained.TryGetValue(questionId, out var item))
                {
                    item.OrderIndex = index;
                }
                else
                {
                    _context.ToeicPracticeSetItems.Add(new ToeicPracticeSetItem
                    {
                        PracticeSetId = practiceSet.Id,
                        QuestionId = questionId,
                        OrderIndex = index
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            var items = await _context.LoadItemsAsync(practiceSet.Id, cancellationToken);

            return practiceSet.ToDetailDto(items);
        }, cancellationToken);

        await _cacheInvalidator.InvalidatePracticeSetsAsync([request.Id], cancellationToken);

        return result;
    }
}
