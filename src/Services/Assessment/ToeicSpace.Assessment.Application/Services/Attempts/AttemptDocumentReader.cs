using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Loads an attempt and returns its learner-safe document, with answer history for the revealed questions.
/// </summary>
public sealed class AttemptDocumentReader
{
    private readonly AttemptContentReader _contentReader;
    private readonly AnswerHistoryReader _historyReader;
    private readonly AttemptAccess _access;

    public AttemptDocumentReader(
        AttemptContentReader contentReader,
        AnswerHistoryReader historyReader,
        AttemptAccess access)
    {
        _contentReader = contentReader;
        _historyReader = historyReader;
        _access = access;
    }

    public async Task<AttemptDto> ReadAsync(ToeicAttempt attempt, CancellationToken cancellationToken)
    {
        var content = await _contentReader.LoadAsync(attempt, trackAnswers: false, cancellationToken);
        var revealedSourceIds = content.Questions
            .Where(entry => attempt.Status == AttemptStatus.Completed || entry.IsChecked)
            .Select(entry => entry.Question.Id)
            .Distinct()
            .ToList();
        var history = await _historyReader.HistoryAsync(attempt.UserId, attempt.Id, revealedSourceIds, cancellationToken);

        return AttemptDocumentFactory.Create(content, _access.Now, history);
    }
}
