using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Grammar;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Services.Grammar;

/// <summary>Learner progress views of the grammar roadmap (topic rows, stage status, the whole table).</summary>
public sealed class GrammarProgressService(IGrammarLearningRepository repository)
{
    /// <summary>null = never submitted (different from 0 points).</summary>
    public static GrammarLessonProgressDto LessonDto(GrammarTopicContent topic, GrammarLessonProgress? progress)
    {
        var best = progress?.QuickCheckBest ?? [];
        var units = new SortedDictionary<string, UnitScoreDto>(StringComparer.Ordinal);
        foreach (var (key, block) in topic.Blocks.Where(b => b.Key.StartsWith(GrammarBlocks.QuickCheckPrefix)))
        {
            var unit = key[GrammarBlocks.QuickCheckPrefix.Length..];
            units[unit] = new UnitScoreDto(best.TryGetValue(unit, out var score) ? Math.Min(score, block.Total) : null,
                block.Total);
        }
        var attempted = units.Values.Where(u => u.Correct is not null).ToList();
        var mini = topic.MiniTest;
        return new GrammarLessonProgressDto(
            progress?.TheoryConfirmedAt is not null,
            progress?.VocabConfirmedAt is not null,
            attempted.Count > 0 ? new ScoreDto(attempted.Sum(u => u.Correct!.Value), units.Values.Sum(u => u.Total)) : null,
            units,
            progress?.MiniTestBest is { } m ? new ScoreDto(Math.Min(m, mini.Total), mini.Total) : null,
            MiniTestPassed(topic, progress),
            mini.PassMark,
            progress?.CompletedAt is not null);
    }

    public static bool MiniTestPassed(GrammarTopicContent topic, GrammarLessonProgress? progress) =>
        progress?.MiniTestBest is { } best && best >= (topic.MiniTest.PassMark ?? 0);

    /// <summary>Theory + vocabulary confirmed + mini test passed → completed (once).</summary>
    public static void MarkCompletedIfDone(GrammarTopicContent topic, GrammarLessonProgress progress, DateTime now)
    {
        if (progress.CompletedAt is null && GrammarUnlockRules.IsTopicCompleted(progress.TheoryConfirmedAt is not null,
                progress.VocabConfirmedAt is not null, MiniTestPassed(topic, progress)))
            progress.CompletedAt = now;
    }

    public static GrammarStageProgressDto StageDto(GrammarStageContent stage, IReadOnlyCollection<GrammarLessonProgress> lessons,
        IReadOnlyCollection<int> scores, bool signedIn)
    {
        var total = stage.Topics.Count;
        var completed = stage.Topics.Count(t => lessons.Any(l => l.Topic == t && l.CompletedAt is not null));
        var passMark = stage.Block.PassMark;
        return new GrammarStageProgressDto(
            signedIn && GrammarUnlockRules.IsUnlocked(completed, total),
            scores.Count > 0 ? scores.Max() : null,
            scores.Any(s => s >= (passMark ?? 0)),
            stage.Block.Total,
            passMark,
            new StageRequirementDto(completed, GrammarUnlockRules.RequiredTopics(total), total));
    }

    public async Task<GrammarStageProgressDto> StageStatusAsync(Guid? learnerId, GrammarStageContent stage,
        CancellationToken ct)
    {
        if (learnerId is not { } id) return StageDto(stage, [], [], false);
        var lessons = await repository.ListLessonProgressAsync(id, ct);
        var scores = await repository.ListStageScoresAsync(id, ct);
        return StageDto(stage, lessons, scores.Where(s => s.Stage == stage.Stage).Select(s => s.Score).ToList(), true);
    }

    public async Task<GrammarProgressDto> ProgressAsync(Guid learnerId, GrammarContentBundle bundle, CancellationToken ct)
    {
        var lessons = await repository.ListLessonProgressAsync(learnerId, ct);
        var scores = await repository.ListStageScoresAsync(learnerId, ct);
        var state = await repository.GetStateAsync(learnerId, false, ct);
        var reviewCount = await repository.CountMistakesAsync(learnerId, ct);
        var byTopic = lessons.ToDictionary(l => l.Topic);
        var rows = bundle.Topics.ToDictionary(t => t.Key, t => LessonDto(t.Value, byTopic.GetValueOrDefault(t.Key)));
        var partsDone = rows.Values.Sum(r => (r.TheoryDone ? 1 : 0) + (r.VocabDone ? 1 : 0) + (r.MiniTestPassed ? 1 : 0));
        var stages = bundle.Stages.ToDictionary(s => s.Key, s => StageDto(s.Value, lessons,
            scores.Where(x => x.Stage == s.Key).Select(x => x.Score).ToList(), true));
        var current = state?.PositionTopic is { } topic && state.PositionPart is { } part
            ? new StudyPositionDto(topic, part, state.PositionAnchor) : null;
        return new GrammarProgressDto(
            new GrammarStatsDto(rows.Values.Count(r => r.Completed), reviewCount,
                GrammarUnlockRules.ProgressPercent(partsDone, bundle.Topics.Count)),
            current, rows, stages);
    }
}
