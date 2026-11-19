using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Grammar;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Services.Grammar;

/// <summary>Writes learner × question results for answered (non-blank) questions and reveals.</summary>
public sealed class GrammarResultRecorder(IGrammarLearningRepository repository)
{
    public async Task RecordAsync(Guid learnerId, IReadOnlyList<(GrammarQuestionInfo Question, bool Correct)> graded,
        DateTime now, CancellationToken ct)
    {
        if (graded.Count == 0) return;
        var existing = await repository.GetQuestionResultsAsync(learnerId, graded.Select(g => g.Question.Id).ToList(), ct);
        var rows = existing.ToDictionary(r => r.QuestionId, StringComparer.Ordinal);
        foreach (var (question, correct) in graded)
        {
            if (!rows.TryGetValue(question.Id, out var row))
            {
                row = NewRow(learnerId, question, now);
                repository.AddQuestionResult(row);
                rows[question.Id] = row;
            }
            row.Record(correct, now);
        }
    }

    public async Task MarkRevealedAsync(Guid learnerId, GrammarQuestionInfo question, DateTime now, CancellationToken ct)
    {
        var row = (await repository.GetQuestionResultsAsync(learnerId, [question.Id], ct)).FirstOrDefault();
        if (row is null)
        {
            row = NewRow(learnerId, question, now);
            row.Revealed = true;
            repository.AddQuestionResult(row);
            return;
        }
        row.MarkRevealed(now);
    }

    private static GrammarQuestionResult NewRow(Guid learnerId, GrammarQuestionInfo question, DateTime now) => new()
    {
        LearnerId = learnerId,
        QuestionId = question.Id,
        Topic = question.MainTopic,
        Block = question.Block,
        UnitCode = question.UnitCode,
        TrapCode = question.TrapCode,
        UpdatedAt = now
    };
}
