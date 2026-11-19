using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.CheckAnswers;

public sealed class CheckAnswersHandler : IRequestHandler<CheckAnswersCommand, RevealDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AttemptContentReader _contentReader;
    private readonly AnswerHistoryReader _history;

    public CheckAnswersHandler(
        IAssessmentDbContext context,
        AttemptAccess access,
        AttemptContentReader contentReader,
        AnswerHistoryReader history)
    {
        _context = context;
        _access = access;
        _contentReader = contentReader;
        _history = history;
    }

    public Task<RevealDto> Handle(CheckAnswersCommand request, CancellationToken cancellationToken)
        => _access.MutateAsync(request.AttemptId, async (attempt, _) =>
        {
            if (attempt.Mode == AttemptMode.FullTest)
            {
                throw AppException.Forbidden("Answers are only revealed after a mock test is submitted.",
                    ErrorCodes.CheckNotAllowed);
            }

            if (attempt.Status != AttemptStatus.InProgress)
            {
                throw AttemptAccess.NotActive();
            }

            var content = await _contentReader.LoadAsync(attempt, trackAnswers: true, cancellationToken);
            var requested = request.QuestionIds.ToHashSet();
            var entries = content.Questions.Where(entry => requested.Contains(entry.Row.Id)).ToList();

            if (entries.Count != requested.Count)
            {
                throw AppException.Validation("Question does not belong to this attempt.");
            }

            var now = _access.Now;
            var checkedEntries = entries
                .Select(entry =>
                {
                    var answer = entry.Answer;

                    if (answer is null)
                    {
                        answer = new ToeicAttemptAnswer
                        {
                            Id = Guid.NewGuid(),
                            AttemptId = attempt.Id,
                            QuestionId = entry.Row.SourceQuestionId,
                            SnapshotQuestionId = entry.Row.Id,
                            CorrectAnswer = entry.Question.CorrectAnswer
                        };
                        _context.ToeicAttemptAnswers.Add(answer);
                    }

                    if (answer.CheckedAt is null)
                    {
                        answer.CheckedAt = now;
                        answer.CorrectAnswer = entry.Question.CorrectAnswer;
                        answer.IsCorrect = answer.UserAnswer == entry.Question.CorrectAnswer;
                    }

                    return entry with { Answer = answer };
                })
                .ToList();

            attempt.LastActivityAt = now;
            await _context.SaveChangesAsync(cancellationToken);

            var history = await _history.HistoryAsync(attempt.UserId, attempt.Id,
                checkedEntries.Select(entry => entry.Question.Id).Distinct().ToList(), cancellationToken);

            return AttemptDocumentFactory.Reveal(content, checkedEntries, history);
        }, cancellationToken);
}
