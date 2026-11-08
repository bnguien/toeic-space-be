using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public sealed record GrammarStageScore(string Stage, int Score);

public sealed record GrammarTrapAggregate(string TrapCode, int WrongCount, int QuestionCount);

/// <summary>Per-learner grammar data: topic progress, attempts, question results and the learner state row.</summary>
public interface IGrammarLearningRepository
{
    Task<GrammarLessonProgress?> GetLessonProgressAsync(Guid learnerId, string topic, CancellationToken ct);
    Task<IReadOnlyList<GrammarLessonProgress>> ListLessonProgressAsync(Guid learnerId, CancellationToken ct);
    void AddLessonProgress(GrammarLessonProgress progress);

    void AddAttempt(GrammarAttempt attempt);
    Task<GrammarAttempt?> GetAttemptAsync(Guid learnerId, long attemptId, CancellationToken ct);
    Task<IReadOnlyList<GrammarStageScore>> ListStageScoresAsync(Guid learnerId, CancellationToken ct);

    Task<IReadOnlyList<GrammarQuestionResult>> GetQuestionResultsAsync(Guid learnerId, IReadOnlyCollection<string> questionIds,
        CancellationToken ct);
    void AddQuestionResult(GrammarQuestionResult result);
    Task<int> CountMistakesAsync(Guid learnerId, CancellationToken ct);
    /// <summary>Questions answered wrong at least once whose latest answer is still wrong, by unit code then id.</summary>
    Task<IReadOnlyList<GrammarQuestionResult>> ListMistakesAsync(Guid learnerId, int limit, CancellationToken ct);
    Task<IReadOnlyList<GrammarTrapAggregate>> ListTrapsAsync(Guid learnerId, int limit, CancellationToken ct);

    Task<GrammarLearnerState?> GetStateAsync(Guid learnerId, bool forUpdate, CancellationToken ct);
    void AddState(GrammarLearnerState state);
}
