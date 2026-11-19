using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.SetQuestionFlag;

public sealed class SetQuestionFlagHandler : IRequestHandler<SetQuestionFlagCommand, QuestionFlagDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;

    public SetQuestionFlagHandler(IAssessmentDbContext context, AttemptAccess access)
    {
        _context = context;
        _access = access;
    }

    public Task<QuestionFlagDto> Handle(SetQuestionFlagCommand request, CancellationToken cancellationToken)
        => _access.MutateAsync(request.AttemptId, async (attempt, _) =>
        {
            if (_access.IsTimeUp(attempt))
            {
                throw AttemptAccess.TimeUp();
            }

            if (attempt.Status != AttemptStatus.InProgress)
            {
                throw AttemptAccess.NotActive();
            }

            var row = await _context.ToeicAttemptQuestions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.AttemptId == attempt.Id && item.Id == request.QuestionId, cancellationToken)
                ?? throw AppException.Validation("Question does not belong to this attempt.");

            var answer = await _context.ToeicAttemptAnswers
                .SingleOrDefaultAsync(item => item.AttemptId == attempt.Id && item.SnapshotQuestionId == row.Id, cancellationToken);

            if (answer is null)
            {
                // A flag on an unanswered question; the answer revision stays 0.
                answer = new ToeicAttemptAnswer
                {
                    Id = Guid.NewGuid(),
                    AttemptId = attempt.Id,
                    QuestionId = row.SourceQuestionId,
                    SnapshotQuestionId = row.Id,
                    CorrectAnswer = AttemptContentReader.ReadQuestion(row).CorrectAnswer
                };
                _context.ToeicAttemptAnswers.Add(answer);
            }

            answer.IsFlagged = request.Flagged;
            attempt.LastActivityAt = _access.Now;
            await _context.SaveChangesAsync(cancellationToken);

            return new QuestionFlagDto(row.Id, answer.IsFlagged);
        }, cancellationToken);
}
