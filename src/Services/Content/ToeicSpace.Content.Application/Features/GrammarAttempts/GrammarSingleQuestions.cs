using ToeicSpace.Content.Application.Models.Grammar;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts;

/// <summary>
/// A single question outside an attempt (mistake book). Questions are shared content (no IDOR); a stage-test question
/// only once its stage is unlocked, otherwise the stage lock could be bypassed.
/// </summary>
public sealed class GrammarSingleQuestions(GrammarContentProvider content, GrammarProgressService progress)
{
    public async Task<GrammarQuestionInfo> GetAsync(Guid? learnerId, string questionId, CancellationToken ct)
    {
        var bundle = await content.GetAsync(ct);
        var question = bundle.Questions.GetValueOrDefault(questionId ?? string.Empty) ?? throw GrammarErrors.QuestionNotFound();
        if (question.Stage is { } stage
            && !(await progress.StageStatusAsync(learnerId, bundle.Stages[stage], ct)).Unlocked)
            throw GrammarErrors.StageLocked();
        return question;
    }
}
