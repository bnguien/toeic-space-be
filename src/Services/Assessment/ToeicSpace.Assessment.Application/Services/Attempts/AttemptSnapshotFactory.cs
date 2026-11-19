using System.Text.Json;
using ToeicSpace.Assessment.Application.Common.Content;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Models.Attempts;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Captures immutable copies of source content and creates the attempt that owns them.
/// Media URLs are replaced by their retained, content-addressed copies.
/// </summary>
public static class AttemptSnapshotFactory
{
    public static QuestionSnapshot Capture(ToeicQuestion question, IReadOnlyDictionary<string, string> media)
        => new(question.Id, question.PassageId, question.Part, question.Version, question.QuestionNumber,
            question.QuestionText, question.OptionA, question.OptionB, question.OptionC, question.OptionD,
            ReplaceMedia(question.AudioUrl, media), ReplaceMedia(question.ImageUrl, media), question.Transcript,
            question.CorrectAnswer, question.Explanation, question.DifficultyLevel, question.Topic);

    public static PassageSnapshot Capture(ToeicPassage passage, IReadOnlyDictionary<string, string> media)
        => new(passage.Id, passage.Part, passage.PassageType, passage.Title, passage.Content, passage.Transcript,
            ReplaceMedia(passage.AudioUrl, media), ReplaceMedia(passage.ImageUrl, media), passage.OrderIndex);

    public static SetSnapshot Capture(ToeicPracticeSet set)
        => new(set.Id, set.Code, set.Title, set.Description, set.Part, set.Kind, set.Level, set.TargetScore,
            set.DurationMinutes);

    public static AttemptSourceSnapshot Source(ToeicTest test)
        => new(AttemptSourceKind.Test, test.Id, test.Code, test.Title, test.Category, null, null, null,
            test.DurationMinutes);

    public static AttemptSourceSnapshot Source(SetSnapshot set)
        => new(AttemptSourceKind.PracticeSet, set.Id, set.Code, set.Title, null, set.Part, set.Level,
            set.TargetScore, set.DurationMinutes);

    /// <summary>
    /// Snapshots the given questions (in that order) and the passages they use, from live source content.
    /// </summary>
    public static (IReadOnlyList<QuestionSnapshot> Questions, IReadOnlyList<PassageSnapshot> Passages) Capture(
        AttemptSourceContent source,
        IReadOnlyList<Guid> questionIds,
        IReadOnlyDictionary<string, string> media)
    {
        var questionsById = source.Questions.ToDictionary(question => question.Id);
        var questions = questionIds.Select(id => Capture(questionsById[id], media)).ToList();
        var passageIds = questions.Where(question => question.PassageId.HasValue)
            .Select(question => question.PassageId!.Value)
            .ToHashSet();
        var passages = source.Passages
            .Where(passage => passageIds.Contains(passage.Id))
            .Select(passage => Capture(passage, media))
            .ToList();

        return (questions, passages);
    }

    /// <summary>Adds the attempt, its snapshot and its question rows to the context (not saved).</summary>
    public static ToeicAttempt Add(IAssessmentDbContext context, NewAttempt draft)
    {
        var parts = draft.Questions.Select(question => question.Part).Distinct().Order().ToList();
        var attempt = new ToeicAttempt
        {
            Id = Guid.NewGuid(),
            UserId = draft.UserId,
            TestId = draft.TestId,
            PracticeSetId = draft.PracticeSetId,
            Mode = draft.Mode,
            Status = AttemptStatus.InProgress,
            Title = draft.Title,
            SelectedParts = AttemptParts.Format(parts),
            StartTime = draft.Now,
            LastActivityAt = draft.Now,
            TimeLimitSeconds = draft.TimeLimitSeconds,
            Deadline = draft.TimeLimitSeconds is { } limit ? draft.Now.AddSeconds(limit) : null,
            TotalQuestions = draft.Questions.Count
        };

        attempt.Snapshot = new ToeicAttemptSnapshot
        {
            AttemptId = attempt.Id,
            CapturedAt = draft.Now,
            Payload = JsonSerializer.Serialize(draft.Document)
        };

        context.ToeicAttempts.Add(attempt);

        for (var index = 0; index < draft.Questions.Count; index++)
        {
            var question = draft.Questions[index];

            context.ToeicAttemptQuestions.Add(new ToeicAttemptQuestion
            {
                Id = Guid.NewGuid(),
                AttemptId = attempt.Id,
                SourceQuestionId = question.Id,
                Version = question.Version,
                OrderIndex = index,
                Payload = JsonSerializer.Serialize(question)
            });
        }

        return attempt;
    }

    private static string? ReplaceMedia(string? value, IReadOnlyDictionary<string, string> media)
        => value == null
            ? null
            : string.Join(MediaUrls.ImageSeparator, MediaUrls.Split(value).Select(url => media[url]));
}
