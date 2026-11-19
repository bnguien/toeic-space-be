using ToeicSpace.Assessment.Application.Common.Content;
using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Models.Attempts;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Builds the attempt document and enforces what a learner may see.
/// Until an attempt is completed: no answer key, explanation, transcript or translation outside
/// <see cref="RevealDto"/> (which only holds checked questions); no Part 1/2 prompt text; no Part 1–4 passage
/// content (Part 3/4 content is the script); Part 6/7 passages in English only.
/// </summary>
public static class AttemptDocumentFactory
{
    public static AttemptDto Create(
        AttemptContent content,
        DateTime now,
        IReadOnlyDictionary<Guid, IReadOnlyList<AnswerHistoryDto>> history)
    {
        var attempt = content.Attempt;
        var completed = attempt.Status == AttemptStatus.Completed;
        var revealed = completed
            ? content.Questions
            : content.Questions.Where(entry => entry.IsChecked).ToList();
        var lastActivity = AttemptAccess.Utc(attempt.LastActivityAt ?? attempt.StartTime);

        return new AttemptDto(
            attempt.Id,
            attempt.Mode,
            attempt.Status,
            attempt.Title,
            Source(content.Source),
            attempt.TestId,
            attempt.PracticeSetId,
            Parts(content),
            AttemptAccess.Utc(attempt.StartTime),
            lastActivity,
            lastActivity.AddDays(AttemptAccess.RetentionDays),
            attempt.Deadline is { } deadline ? AttemptAccess.Utc(deadline) : null,
            attempt.TimeLimitSeconds,
            AttemptAccess.Utc(now),
            content.Questions.Select(entry => Question(entry, completed)).ToList(),
            PassagesOf(content, content.Questions).Select(passage => Passage(passage, completed)).ToList(),
            Reveal(content, revealed, history));
    }

    public static RevealDto Reveal(
        AttemptContent content,
        IReadOnlyCollection<AttemptQuestionEntry> entries,
        IReadOnlyDictionary<Guid, IReadOnlyList<AnswerHistoryDto>> history)
    {
        var questions = entries
            .Select(entry => new QuestionRevealDto(
                entry.Row.Id,
                entry.Question.CorrectAnswer,
                entry.Answer?.UserAnswer,
                entry.IsCorrect,
                entry.Question.Explanation,
                entry.Question.Transcript,
                history.GetValueOrDefault(entry.Question.Id) ?? []))
            .ToList();

        var passages = PassagesOf(content, entries)
            .Select(passage => new PassageRevealDto(
                passage.Id,
                TranslatedText.PassageText(passage.Content) ?? TranslatedText.TranscriptText(passage.Transcript),
                TranslatedText.PassageTranslation(passage.Content) ?? TranslatedText.TranscriptTranslation(passage.Transcript)))
            .ToList();

        return new RevealDto(questions, passages);
    }

    public static AttemptSourceDto Source(AttemptSourceSnapshot source)
        => new(source.Kind, source.Id, source.Code, source.Title, source.Category,
            source.Part is { } part ? (int)part : null, source.Level, source.TargetScore);

    public static IReadOnlyList<int> Parts(AttemptContent content)
    {
        var stored = AttemptParts.Parse(content.Attempt.SelectedParts);

        return stored.Count > 0
            ? stored.Select(part => (int)part).ToList()
            : content.Questions.Select(entry => (int)entry.Question.Part).Distinct().Order().ToList();
    }

    private static AttemptQuestionDto Question(AttemptQuestionEntry entry, bool completed)
    {
        var question = entry.Question;
        var answer = entry.Answer;
        // Part 1/2 prompts are only heard in the real test.
        var promptHidden = !completed && question.Part is ToeicPart.Part1 or ToeicPart.Part2;

        return new AttemptQuestionDto(
            entry.Row.Id,
            question.Id,
            question.PassageId,
            (int)question.Part,
            entry.Row.OrderIndex,
            question.QuestionNumber,
            promptHidden ? null : question.QuestionText,
            question.OptionA,
            question.OptionB,
            question.OptionC,
            question.OptionD,
            question.AudioUrl,
            question.ImageUrl,
            answer?.UserAnswer,
            answer?.Revision ?? 0,
            answer?.IsFlagged ?? false,
            answer?.CheckedAt is not null,
            answer?.TimeSpentSeconds ?? 0);
    }

    private static AttemptPassageDto Passage(PassageSnapshot passage, bool completed)
    {
        string? content;

        if (completed)
        {
            content = TranslatedText.PassageText(passage.Content) ?? TranslatedText.TranscriptText(passage.Transcript);
        }
        else
        {
            content = ToeicPartRules.GetSection(passage.Part) == ToeicSection.Listening
                ? null
                : TranslatedText.PassageText(passage.Content);
        }

        return new AttemptPassageDto(
            passage.Id,
            (int)passage.Part,
            passage.PassageType,
            passage.Title,
            content,
            passage.AudioUrl,
            passage.ImageUrl,
            passage.OrderIndex);
    }

    /// <summary>Passages referenced by the given questions, in the order they first appear.</summary>
    private static IEnumerable<PassageSnapshot> PassagesOf(
        AttemptContent content,
        IEnumerable<AttemptQuestionEntry> entries)
        => entries
            .Where(entry => entry.Question.PassageId.HasValue)
            .Select(entry => entry.Question.PassageId!.Value)
            .Distinct()
            .Where(content.PassagesById.ContainsKey)
            .Select(id => content.PassagesById[id]);
}
