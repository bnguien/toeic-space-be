using System.Text.Json.Nodes;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RevealAttemptAnswer;

/// <summary>The full answer and explanation of one question of an attempt (marks it revealed when signed in).</summary>
public sealed record RevealAttemptAnswerCommand(string QuestionId) : IRequest<JsonObject>
{
    public string AttemptId { get; init; } = string.Empty;
    public Guid? LearnerId { get; init; }
}
