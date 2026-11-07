using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.AnswerQuestion;

public sealed class AnswerQuestionHandler(GrammarSingleQuestions questions, GrammarResultRecorder recorder,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<AnswerQuestionCommand, AnswerResultDto>
{
    public async Task<AnswerResultDto> Handle(AnswerQuestionCommand request, CancellationToken ct)
    {
        var question = await questions.GetAsync(request.LearnerId, request.QuestionId, ct);
        GrammarAnswerGrading.CheckValue(request.Value);
        var correct = GrammarGrader.Grade(question.Question, request.Value, question.IsClozeBlank);
        var result = GrammarAnswerGrading.ResultOf(question, correct);
        if (request.LearnerId is not { } learnerId || GrammarGrader.IsBlank(request.Value))
            return new AnswerResultDto(result.Correct, result.Explanation, request.LearnerId is not null);
        await recorder.RecordAsync(learnerId, [(question, correct)], clock.GetUtcNow().UtcDateTime, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new AnswerResultDto(result.Correct, result.Explanation, true);
    }
}
