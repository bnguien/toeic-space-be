using System.Text.Json.Nodes;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Services.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RevealQuestion;

public sealed class RevealQuestionHandler(GrammarSingleQuestions questions, GrammarResultRecorder recorder,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<RevealQuestionCommand, JsonObject>
{
    public async Task<JsonObject> Handle(RevealQuestionCommand request, CancellationToken ct)
    {
        var question = await questions.GetAsync(request.LearnerId, request.QuestionId, ct);
        if (request.LearnerId is { } learnerId)
        {
            await recorder.MarkRevealedAsync(learnerId, question, clock.GetUtcNow().UtcDateTime, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return GrammarContentBuilder.AnswerOf(question);
    }
}
