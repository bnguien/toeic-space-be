using System.Text.Json.Nodes;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Services.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RevealAttemptAnswer;

public sealed class RevealAttemptAnswerHandler(GrammarAttemptResolver resolver, GrammarResultRecorder recorder,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<RevealAttemptAnswerCommand, JsonObject>
{
    public async Task<JsonObject> Handle(RevealAttemptAnswerCommand request, CancellationToken ct)
    {
        var (attempt, _, question) = await resolver.ResolveAsync(request.LearnerId, request.AttemptId, request.QuestionId, ct);
        if (attempt is not null)
        {
            await recorder.MarkRevealedAsync(attempt.LearnerId, question, clock.GetUtcNow().UtcDateTime, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return GrammarContentBuilder.AnswerOf(question);
    }
}
