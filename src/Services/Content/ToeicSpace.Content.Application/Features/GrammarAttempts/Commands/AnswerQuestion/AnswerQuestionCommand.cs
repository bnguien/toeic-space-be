using System.Text.Json.Nodes;
using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.AnswerQuestion;

/// <summary>Grades one question outside any attempt (mistake book): no attempt row, no attempt count, no best score.</summary>
public sealed record AnswerQuestionCommand(JsonNode? Value) : IRequest<AnswerResultDto>
{
    public string QuestionId { get; init; } = string.Empty;
    public Guid? LearnerId { get; init; }
}
