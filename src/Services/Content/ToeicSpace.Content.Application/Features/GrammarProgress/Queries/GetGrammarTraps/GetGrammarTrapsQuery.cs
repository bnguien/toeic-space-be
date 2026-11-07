using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarTraps;

/// <summary>The learner's most frequent traps (top 10), grouped by each question's main trap code.</summary>
public sealed record GetGrammarTrapsQuery(Guid LearnerId) : IRequest<IReadOnlyList<TrapDto>>
{
    public const int Top = 10;
}
