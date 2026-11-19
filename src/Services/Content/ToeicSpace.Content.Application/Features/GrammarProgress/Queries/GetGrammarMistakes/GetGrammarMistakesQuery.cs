using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarMistakes;

/// <summary>Mistake book: questions answered wrong whose latest answer is still wrong, grouped by unit (code order).</summary>
public sealed record GetGrammarMistakesQuery(Guid LearnerId) : IRequest<IReadOnlyList<MistakeGroupDto>>
{
    public const int MaxQuestions = 500;
}
