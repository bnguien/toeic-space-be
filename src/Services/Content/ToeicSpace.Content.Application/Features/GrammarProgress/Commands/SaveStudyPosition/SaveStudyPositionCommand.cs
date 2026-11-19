using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.SaveStudyPosition;

/// <summary>Remembers where the learner is (topic, part, anchor ≤ 120 chars) for "resume".</summary>
public sealed record SaveStudyPositionCommand(string Topic, string Part, string? Anchor) : IRequest<PositionResultDto>
{
    public static readonly string[] Parts = ["theory", "vocab", "exercises"];
    public Guid LearnerId { get; init; }
}
