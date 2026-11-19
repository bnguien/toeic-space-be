using System.Text.Json;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Models.Attempts;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Loads an attempt's immutable snapshot (questions, passages, answer key) together with its answer rows.
/// </summary>
public sealed class AttemptContentReader
{
    private readonly IAssessmentDbContext _context;

    public AttemptContentReader(IAssessmentDbContext context)
    {
        _context = context;
    }

    /// <param name="trackAnswers">True when the caller modifies answer rows (grading, checking).</param>
    public async Task<AttemptContent> LoadAsync(
        ToeicAttempt attempt,
        bool trackAnswers,
        CancellationToken cancellationToken)
    {
        var snapshot = await _context.ToeicAttemptSnapshots
            .AsNoTracking()
            .SingleAsync(item => item.AttemptId == attempt.Id, cancellationToken);
        var document = JsonSerializer.Deserialize<AttemptSnapshotDocument>(snapshot.Payload)
            ?? throw new InvalidOperationException("Attempt snapshot payload is empty.");

        var rows = await _context.ToeicAttemptQuestions
            .AsNoTracking()
            .Where(item => item.AttemptId == attempt.Id)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);

        var answerQuery = _context.ToeicAttemptAnswers
            .Where(item => item.AttemptId == attempt.Id && item.SnapshotQuestionId != null);
        var answers = await (trackAnswers ? answerQuery : answerQuery.AsNoTracking())
            .ToDictionaryAsync(item => item.SnapshotQuestionId!.Value, cancellationToken);

        var entries = rows
            .Select(row => new AttemptQuestionEntry(row, ReadQuestion(row), answers.GetValueOrDefault(row.Id)))
            .ToList();

        return new AttemptContent(attempt, document, entries);
    }

    public static QuestionSnapshot ReadQuestion(ToeicAttemptQuestion row)
        => JsonSerializer.Deserialize<QuestionSnapshot>(row.Payload)
            ?? throw new InvalidOperationException("Attempt question payload is empty.");
}
