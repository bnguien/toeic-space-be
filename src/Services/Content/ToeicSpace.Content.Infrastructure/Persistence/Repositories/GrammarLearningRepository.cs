using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class GrammarLearningRepository(ContentDbContext db) : IGrammarLearningRepository
{
    public Task<GrammarLessonProgress?> GetLessonProgressAsync(Guid learnerId, string topic, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.GrammarLessonProgress.FirstOrDefaultAsync(x => x.LearnerId == learnerId && x.Topic == topic, ct));

    public async Task<IReadOnlyList<GrammarLessonProgress>> ListLessonProgressAsync(Guid learnerId, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.GrammarLessonProgress.AsNoTracking().Where(x => x.LearnerId == learnerId).ToListAsync(ct));

    public void AddLessonProgress(GrammarLessonProgress progress) => db.GrammarLessonProgress.Add(progress);

    public void AddAttempt(GrammarAttempt attempt) => db.GrammarAttempts.Add(attempt);

    public Task<GrammarAttempt?> GetAttemptAsync(Guid learnerId, long attemptId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.GrammarAttempts.FirstOrDefaultAsync(x => x.Id == attemptId && x.LearnerId == learnerId, ct));

    public async Task<IReadOnlyList<GrammarStageScore>> ListStageScoresAsync(Guid learnerId, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarAttempts.AsNoTracking()
            .Where(x => x.LearnerId == learnerId && x.Stage != null)
            .Select(x => new GrammarStageScore(x.Stage!, x.Score)).ToListAsync(ct));

    public async Task<IReadOnlyList<GrammarQuestionResult>> GetQuestionResultsAsync(Guid learnerId,
        IReadOnlyCollection<string> questionIds, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarQuestionResults
            .Where(x => x.LearnerId == learnerId && questionIds.Contains(x.QuestionId)).ToListAsync(ct));

    public void AddQuestionResult(GrammarQuestionResult result) => db.GrammarQuestionResults.Add(result);

    public Task<int> CountMistakesAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarQuestionResults
            .CountAsync(x => x.LearnerId == learnerId && !x.LastCorrect && x.WrongCount > 0, ct));

    public async Task<IReadOnlyList<GrammarQuestionResult>> ListMistakesAsync(Guid learnerId, int limit, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarQuestionResults.AsNoTracking()
            .Where(x => x.LearnerId == learnerId && !x.LastCorrect && x.WrongCount > 0)
            .OrderBy(x => x.UnitCode).ThenBy(x => x.QuestionId).Take(limit).ToListAsync(ct));

    public async Task<IReadOnlyList<GrammarTrapAggregate>> ListTrapsAsync(Guid learnerId, int limit, CancellationToken ct)
    {
        var rows = await PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarQuestionResults.AsNoTracking()
            .Where(x => x.LearnerId == learnerId && x.WrongCount > 0 && x.TrapCode != "")
            .GroupBy(x => x.TrapCode)
            .Select(g => new { TrapCode = g.Key, WrongCount = g.Sum(x => x.WrongCount), QuestionCount = g.Count() })
            .OrderByDescending(x => x.WrongCount).ThenBy(x => x.TrapCode).Take(limit).ToListAsync(ct));
        return rows.Select(x => new GrammarTrapAggregate(x.TrapCode, x.WrongCount, x.QuestionCount)).ToList();
    }

    public Task<GrammarLearnerState?> GetStateAsync(Guid learnerId, bool forUpdate, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => (forUpdate ? db.GrammarLearnerStates : db.GrammarLearnerStates.AsNoTracking())
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId, ct));

    public void AddState(GrammarLearnerState state) => db.GrammarLearnerStates.Add(state);
}
