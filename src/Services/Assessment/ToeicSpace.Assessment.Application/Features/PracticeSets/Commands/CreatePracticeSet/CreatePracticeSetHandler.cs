using ToeicSpace.Assessment.Application.Common.Querying;
using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.DTOs.PracticeSets;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Mappings;
using ToeicSpace.Assessment.Application.Services;

namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.CreatePracticeSet;

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
        PracticeSetAuthorization.EnsureCanCreate(_currentUser);
        var practiceSetId = Guid.NewGuid();
        return await _context.ExecutePracticeSetUpdateAsync(practiceSetId, async () =>
        {
            await _context.EnsureCodeIsAvailableAsync(request.Code.Trim(), currentPracticeSetId: null, cancellationToken);
            var questionIds = request.QuestionIds ?? [];
            await _context.LockPracticeContentAsync(questionIds.ToArray(), cancellationToken);
            var questions = await _context.ToeicQuestions.AsNoTracking()
                .Where(question => questionIds.Contains(question.Id))
                .Select(question => new { question.Id, question.Part })
                .ToListAsync(cancellationToken);
            if (questionIds.Except(questions.Select(question => question.Id)).Any()
                || questions.Any(question => question.Part != request.Part))
            {
                throw AppException.Validation(
                    "Every selected question must exist and belong to the practice set's part.",
                    ErrorCodes.InvalidReference);
            }

            var practiceSet = new ToeicPracticeSet
            {
                Id = practiceSetId,
                Status = request.Status,
                CreatedByUserId = _currentUser.UserId
            };

            PracticeSetContentValidator.ApplyContent(practiceSet, request);
            if (practiceSet.Status == ContentStatus.Active)
                await PracticeSetPublishingGuard.EnsureValidAsync(
                    _context, practiceSet, questionIds, cancellationToken, lockContent: false);

            practiceSet.Items = questionIds.Select((questionId, index) => new ToeicPracticeSetItem
            {
                PracticeSetId = practiceSet.Id,
                QuestionId = questionId,
                OrderIndex = index
            }).ToList();
            _context.ToeicPracticeSets.Add(practiceSet);
            // Metadata, status and ordered questions commit together, after all validation.
            await _context.SaveChangesAsync(cancellationToken);

            return practiceSet.ToDetailDto(await _context.LoadItemsAsync(practiceSet.Id, cancellationToken));
        }, cancellationToken);
    }
}
