using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Models.Attempts;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

public static class AttemptResultFactory
{
    public static AttemptResultDto Create(AttemptContent content)
    {
        var attempt = content.Attempt;

        return new AttemptResultDto(
            attempt.Id,
            attempt.Mode,
            attempt.Status,
            attempt.Title,
            AttemptDocumentFactory.Source(content.Source),
            AttemptDocumentFactory.Parts(content),
            AttemptAccess.Utc(attempt.StartTime),
            attempt.EndTime is { } endTime ? AttemptAccess.Utc(endTime) : null,
            attempt.DurationSeconds,
            AttemptScoreCalculator.Score(content),
            AttemptScoreCalculator.ByPart(content),
            content.Questions
                .Select(entry => new AnswerResultDto(
                    entry.Row.Id,
                    entry.Question.Id,
                    entry.Question.QuestionNumber,
                    (int)entry.Question.Part,
                    entry.Row.OrderIndex,
                    entry.Answer?.UserAnswer,
                    entry.Question.CorrectAnswer,
                    entry.IsCorrect,
                    entry.Answer?.IsFlagged ?? false))
                .ToList());
    }
}
