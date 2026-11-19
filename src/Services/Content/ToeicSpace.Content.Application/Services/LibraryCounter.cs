using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Services;

/// <summary>Word / learning / mastered / due counts of a group of saved words.</summary>
public static class LibraryCounter
{
    public static LibraryCountsDto Count(IEnumerable<SavedWord> words,
        IReadOnlyDictionary<Guid, LearnerWordProgress> progress, DateOnly localToday)
    {
        int total = 0, learning = 0, mastered = 0, due = 0;
        foreach (var word in words)
        {
            total++;
            if (!progress.TryGetValue(word.WordId, out var row)) continue;
            if (row.IntervalDays >= SpacedRepetitionScheduler.MasteredIntervalDays) mastered++;
            else learning++;
            if (SpacedRepetitionScheduler.IsDue(row.DueOn, localToday)) due++;
        }
        return new LibraryCountsDto(total, learning, mastered, due);
    }
}
