using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Mappings;

public static class VocabularyMappingExtensions
{
    public const int PreviewWordCount = 3;

    public static StageDto ToDto(this VocabularyStage x, int setCount, int wordCount) =>
        new(x.Id, x.Slug, x.Title, x.Description, x.Order, x.ToeicTarget, setCount, wordCount);

    public static SetSummaryDto ToSummaryDto(this VocabularySet x)
    {
        var words = x.Words.OrderBy(w => w.Order).ToArray();
        return new SetSummaryDto(x.Id, x.StageId, x.Slug, x.Title, x.Order, words.Length,
            words.Take(PreviewWordCount).Select(w => w.Word!.Headword).ToArray(),
            words.Length > 0 ? words[^1].Word!.Headword : string.Empty);
    }

    public static SetDetailDto ToDetailDto(this VocabularySet x, Guid? previousSetId, Guid? nextSetId) =>
        new(x.Id, x.StageId, x.Stage!.Slug, x.Stage.Title, x.Slug, x.Title, x.Order, previousSetId, nextSetId,
            x.Words.OrderBy(w => w.Order).Select(w => w.Word!.ToDto()).ToArray());

    public static WordDto ToDto(this VocabularyWord x) => new(x.Id, x.Headword, x.Phonetic, x.PartOfSpeech,
        x.MeaningVi, x.DefinitionEn, x.Example, x.ExampleVi, x.Cefr, x.Synonyms.ToArray());

    /// <summary>Progress of a word; a null row is a <see cref="WordStatus.New"/> word.</summary>
    public static ProgressDto ToProgressDto(this LearnerWordProgress? x, DateOnly localToday) => x is null
        ? new ProgressDto(WordStatus.New, 0, 0, null, false, null)
        : new ProgressDto(SpacedRepetitionScheduler.StatusOf(x.IntervalDays), x.IntervalDays, x.Repetitions, x.DueOn,
            SpacedRepetitionScheduler.IsDue(x.DueOn, localToday),
            x.LastReviewedAt is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null);

    public static SchedulePreviewDto ToPreviewDto(this LearnerWordProgress? x, DateOnly localToday)
    {
        var preview = SpacedRepetitionScheduler.Preview(x?.ToState(), localToday);
        return new SchedulePreviewDto(preview.Forget, preview.Hard, preview.Good, preview.Easy);
    }

    public static StudyItemDto ToStudyItemDto(this VocabularyWord word, LearnerWordProgress? progress,
        DateOnly localToday, Guid? savedWordId) =>
        new(word.ToDto(), progress.ToProgressDto(localToday), progress.ToPreviewDto(localToday), savedWordId);
}
