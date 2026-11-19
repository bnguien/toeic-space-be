using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.SaveAnswer;

public sealed class SaveAnswerHandler : IRequestHandler<SaveAnswerCommand, SavedAnswerDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;

    public SaveAnswerHandler(IAssessmentDbContext context, AttemptAccess access)
    {
        _context = context;
        _access = access;
    }

    public Task<SavedAnswerDto> Handle(SaveAnswerCommand request, CancellationToken cancellationToken)
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
            var question = AttemptContentReader.ReadQuestion(row);

            if (request.Answer is { } option && !ToeicPartRules.IsValidAnswer(question.Part, option))
            {
                throw AppException.Validation("Answer option is invalid for this part.");
            }

            var answer = await _context.ToeicAttemptAnswers
                .SingleOrDefaultAsync(item => item.AttemptId == attempt.Id && item.SnapshotQuestionId == row.Id, cancellationToken);

            if (answer?.LastMutationId == request.MutationId)
            {
                // A retried request that was already applied: answer it again without applying twice.
                if (answer.UserAnswer != request.Answer)
                {
                    throw AppException.Conflict("Mutation ID was already used.", ErrorCodes.MutationConflict);
                }

                return Saved(row.Id, answer, attempt);
            }

            if (answer?.CheckedAt is not null)
            {
                throw AppException.Conflict("This question was checked; its answer can no longer change.", ErrorCodes.AnswerLocked);
            }

            if ((answer?.Revision ?? 0) != request.ExpectedRevision)
            {
                throw AppException.Conflict("Answer changed in another session. Reload before choosing which answer to keep.",
                    ErrorCodes.AnswerRevisionConflict);
            }

            if (answer is null)
            {
                answer = new ToeicAttemptAnswer
                {
                    Id = Guid.NewGuid(),
                    AttemptId = attempt.Id,
                    QuestionId = row.SourceQuestionId,
                    SnapshotQuestionId = row.Id,
                    CorrectAnswer = question.CorrectAnswer
                };
                _context.ToeicAttemptAnswers.Add(answer);
            }

            answer.UserAnswer = request.Answer;
            answer.Revision++;
            answer.LastMutationId = request.MutationId;
            answer.TimeSpentSeconds = (int)Math.Min(int.MaxValue, (long)answer.TimeSpentSeconds + (request.TimeSpentSeconds ?? 0));
            attempt.LastActivityAt = _access.Now;
            await _context.SaveChangesAsync(cancellationToken);

            return Saved(row.Id, answer, attempt);
        }, cancellationToken);

    private static SavedAnswerDto Saved(Guid questionId, ToeicAttemptAnswer answer, ToeicAttempt attempt)
        => new(questionId, answer.UserAnswer, answer.Revision, AttemptAccess.Utc(attempt.LastActivityAt!.Value),
            answer.TimeSpentSeconds);
}
