using System.Text.Json.Nodes;
using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.SubmitStageAttempt;

/// <summary>Submits a stage test — only once unlocked (guests are always locked: 403 <c>stage_locked</c>).</summary>
public sealed record SubmitStageAttemptCommand(JsonObject? Answers) : IRequest<AttemptResultDto>
{
    public string Stage { get; init; } = string.Empty;
    public Guid? LearnerId { get; init; }
}
