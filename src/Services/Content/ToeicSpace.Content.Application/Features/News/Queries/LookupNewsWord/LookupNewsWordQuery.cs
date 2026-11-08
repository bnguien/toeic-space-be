using ToeicSpace.Content.Application.DTOs.VocabularyLookup;

namespace ToeicSpace.Content.Application.Features.News.Queries.LookupNewsWord;

/// <summary>
/// Word lookup for guests of the news reader: the fast enrichment tiers only (cache, catalog, dictionary,
/// translation), never AI. Signed-in learners use the vocabulary <c>lookup</c> endpoint, which may use AI.
/// </summary>
public sealed record LookupNewsWordQuery(string Text, string? Context = null) : IRequest<WordLookupDto>;
