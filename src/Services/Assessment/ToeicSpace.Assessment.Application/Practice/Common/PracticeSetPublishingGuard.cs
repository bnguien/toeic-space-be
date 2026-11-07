using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Passages.Commands.CreatePassage;
using ToeicSpace.Assessment.Application.Passages.Common;
using ToeicSpace.Assessment.Application.Questions.Commands.CreateQuestion;
using ToeicSpace.Assessment.Application.Questions.Common;

namespace ToeicSpace.Assessment.Application.Practice.Common;

public static class PracticeSetPublishingGuard
{
    public static async Task EnsureValidAsync(
        IAssessmentDbContext context,
        ToeicPracticeSet practiceSet,
        IReadOnlyList<Guid> questionIds,
        CancellationToken cancellationToken,
        bool lockContent = true)
    {
        if (lockContent)
        {
            await context.LockPracticeContentAsync(questionIds, cancellationToken);
        }
        if (practiceSet.Kind == PracticeSetKind.Level
            ? practiceSet.Level is null or < 1 or > 5
            : practiceSet.Level.HasValue)
        {
            throw NotPublishable("Level must be 1–5 for Level sets and null for other kinds.");
        }

        var questions = await context.ToeicQuestions.AsNoTracking()
            .Where(question => questionIds.Contains(question.Id))
            .ToListAsync(cancellationToken);
        if (questionIds.Count == 0 || questions.Count != questionIds.Count)
        {
            throw NotPublishable("A published practice set needs at least one question; all selected questions must exist.");
        }

        var passageIds = questions.Where(question => question.PassageId.HasValue)
            .Select(question => question.PassageId!.Value).Distinct().ToList();
        var passages = await context.ToeicPassages.AsNoTracking()
            .Where(passage => passageIds.Contains(passage.Id))
            .ToDictionaryAsync(passage => passage.Id, cancellationToken);
        var questionValidator = new QuestionContentValidator();
        var passageValidator = new PassageContentValidator();

        var validatedPassages = new HashSet<Guid>();
        foreach (var question in questions)
        {
            if (question.Part != practiceSet.Part || question.Status != ContentStatus.Active)
            {
                throw NotPublishable($"Question {question.Id} must be an Active Part {(int)practiceSet.Part} question.");
            }

            var content = new CreateQuestionCommand(
                question.TestId, question.PassageId, question.Part, question.QuestionNumber,
                question.QuestionText, question.AudioUrl, question.ImageUrl,
                question.OptionA, question.OptionB, question.OptionC, question.OptionD,
                question.CorrectAnswer, question.Explanation, question.Transcript,
                question.DifficultyLevel, question.Topic, question.OrderIndex, question.PreferAiExplanation);
            var validation = await questionValidator.ValidateAsync(content, cancellationToken);
            if (!validation.IsValid)
            {
                throw NotPublishable($"Question {question.Id}: {validation.Errors[0].ErrorMessage}");
            }

            ToeicPassage? passage = null;
            if (question.PassageId is { } passageId)
            {
                if (!passages.TryGetValue(passageId, out passage)
                    || passage.Part != question.Part || passage.TestId != question.TestId)
                {
                    throw NotPublishable($"Question {question.Id} has a missing or incompatible passage.");
                }

                if (validatedPassages.Add(passage.Id))
                {
                    var passageContent = new CreatePassageCommand(
                        passage.TestId, passage.Part, passage.PassageType, passage.Title,
                        passage.Content, passage.AudioUrl, passage.ImageUrl, passage.Transcript, passage.OrderIndex);
                    var passageValidation = await passageValidator.ValidateAsync(passageContent, cancellationToken);
                    if (!passageValidation.IsValid)
                    {
                        throw NotPublishable($"Passage {passage.Id}: {passageValidation.Errors[0].ErrorMessage}");
                    }
                }
            }

            if (question.Part <= ToeicPart.Part4
                && string.IsNullOrWhiteSpace(question.AudioUrl) && string.IsNullOrWhiteSpace(passage?.AudioUrl))
            {
                throw NotPublishable($"Listening question {question.Id} needs question or passage audio.");
            }

            if (question.Part is ToeicPart.Part3 or ToeicPart.Part4 or ToeicPart.Part5 or ToeicPart.Part7
                && string.IsNullOrWhiteSpace(question.QuestionText))
            {
                throw NotPublishable($"Question {question.Id} needs question text.");
            }
        }
    }

    private static AppException NotPublishable(string message)
        => AppException.Conflict(message, ErrorCodes.PracticeSetNotPublishable);
}
