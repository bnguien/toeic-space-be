using System.Text.Json.Nodes;
using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.SubmitTopicAttempt;

/// <summary>Submits a quick check (<c>qc-u</c>) or the mini test (<c>mt</c>) of a topic. Guests are graded only.</summary>
public sealed record SubmitTopicAttemptCommand(string Block, JsonObject? Answers) : IRequest<AttemptResultDto>
{
    public string Topic { get; init; } = string.Empty;
    public Guid? LearnerId { get; init; }
}
