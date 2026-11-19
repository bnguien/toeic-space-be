using System.Text.Json.Nodes;
using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RetryAttemptAnswer;

/// <summary>Fixes one question of an attempt in place; the attempt score never changes.</summary>
public sealed record RetryAttemptAnswerCommand(string QuestionId, JsonNode? Value) : IRequest<AnswerResultDto>
{
    public string AttemptId { get; init; } = string.Empty;
    public Guid? LearnerId { get; init; }
}
