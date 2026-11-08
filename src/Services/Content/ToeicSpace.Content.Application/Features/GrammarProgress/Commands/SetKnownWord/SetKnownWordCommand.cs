using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.SetKnownWord;

/// <summary>Marks / unmarks a vocabulary word of the topic as known (idempotent).</summary>
public sealed record SetKnownWordCommand(string Topic, string Word, bool Known) : IRequest<KnownWordResultDto>
{
    public Guid LearnerId { get; init; }
}
