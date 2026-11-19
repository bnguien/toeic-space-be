using ToeicSpace.Assessment.Application.Models.Attempts;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Chooses the questions of a practice session. Questions sharing a passage (Part 3/4/6/7) always stay together,
/// so the requested count is a soft target: units are added until it is reached.
/// </summary>
public static class PracticeSelectionPlanner
{
    public const int DefaultCount = 20;

    public const int MinimumCount = 5;

    public const int MaximumCount = 200;

    /// <summary>Groups questions into units (a passage group or a single question) in first-appearance order.</summary>
    public static IReadOnlyList<IReadOnlyList<Guid>> Units(IReadOnlyList<SelectionCandidate> ordered)
    {
        var units = new List<List<Guid>>();
        var byPassage = new Dictionary<Guid, List<Guid>>();

        foreach (var candidate in ordered)
        {
            if (candidate.PassageId is { } passageId)
            {
                if (!byPassage.TryGetValue(passageId, out var group))
                {
                    group = [];
                    byPassage[passageId] = group;
                    units.Add(group);
                }

                group.Add(candidate.QuestionId);
            }
            else
            {
                units.Add([candidate.QuestionId]);
            }
        }

        return units;
    }

    /// <param name="answered">Source questions the learner has ever answered.</param>
    /// <param name="wrong">Source questions whose latest graded answer is wrong.</param>
    public static IReadOnlyList<Guid> Plan(
        IReadOnlyList<SelectionCandidate> ordered,
        PracticeSelection selection,
        int? count,
        IReadOnlySet<Guid> answered,
        IReadOnlySet<Guid> wrong,
        Random random)
    {
        var units = Units(ordered);
        bool HasUnanswered(IReadOnlyList<Guid> unit) => unit.Any(id => !answered.Contains(id));

        return selection switch
        {
            PracticeSelection.Continue => Take(units.Where(HasUnanswered), count ?? DefaultCount),
            PracticeSelection.Random => Take(
                Shuffle(units.Where(HasUnanswered), random)
                    .Concat(Shuffle(units.Where(unit => !HasUnanswered(unit)), random)),
                count ?? DefaultCount),
            PracticeSelection.Mistakes => Take(units.Where(unit => unit.Any(wrong.Contains)), count ?? int.MaxValue),
            _ => ordered.Select(candidate => candidate.QuestionId).ToList()
        };
    }

    /// <summary>
    /// Every question of each unit that contains at least one of <paramref name="questionIds"/>, in source order.
    /// </summary>
    public static IReadOnlyList<Guid> ExpandToUnits(
        IReadOnlyList<SelectionCandidate> ordered,
        IReadOnlySet<Guid> questionIds)
        => Units(ordered)
            .Where(unit => unit.Any(questionIds.Contains))
            .SelectMany(unit => unit)
            .ToList();

    private static List<Guid> Take(IEnumerable<IReadOnlyList<Guid>> units, int target)
    {
        var selected = new List<Guid>();

        foreach (var unit in units)
        {
            if (selected.Count >= target)
            {
                break;
            }

            selected.AddRange(unit);
        }

        return selected;
    }

    private static IEnumerable<IReadOnlyList<Guid>> Shuffle(IEnumerable<IReadOnlyList<Guid>> units, Random random)
    {
        var items = units.ToArray();
        random.Shuffle(items);
        return items;
    }
}
