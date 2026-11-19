using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarProgress;

/// <summary>Stats, resume position, every topic row and every stage status of the learner.</summary>
public sealed record GetGrammarProgressQuery(Guid LearnerId) : IRequest<GrammarProgressDto>;
