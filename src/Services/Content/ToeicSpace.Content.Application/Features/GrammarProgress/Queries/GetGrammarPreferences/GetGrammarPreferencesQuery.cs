using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarPreferences;

public sealed record GetGrammarPreferencesQuery(Guid LearnerId) : IRequest<GrammarPreferencesDto>;
