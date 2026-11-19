using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RetryAttemptAnswer;

public sealed class RetryAttemptAnswerHandler(GrammarAttemptResolver resolver, GrammarResultRecorder recorder,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<RetryAttemptAnswerCommand, AnswerResultDto>
{
    public async Task<AnswerResultDto> Handle(RetryAttemptAnswerCommand request, CancellationToken ct)
    {
        var (attempt, _, question) = await resolver.ResolveAsync(request.LearnerId, request.AttemptId, request.QuestionId, ct);
        GrammarAnswerGrading.CheckValue(request.Value);
        var correct = GrammarGrader.Grade(question.Question, request.Value, question.IsClozeBlank);
        var result = GrammarAnswerGrading.ResultOf(question, correct);
        if (attempt is null || GrammarGrader.IsBlank(request.Value))
            return new AnswerResultDto(result.Correct, result.Explanation, attempt is not null);
        var now = clock.GetUtcNow().UtcDateTime;
        await recorder.RecordAsync(attempt.LearnerId, [(question, correct)], now, ct);
        if (correct && !attempt.FixedQuestionIds.Contains(question.Id))
            attempt.FixedQuestionIds = [.. attempt.FixedQuestionIds, question.Id];
        await unitOfWork.SaveChangesAsync(ct);
        return new AnswerResultDto(result.Correct, result.Explanation, true);
    }
}
