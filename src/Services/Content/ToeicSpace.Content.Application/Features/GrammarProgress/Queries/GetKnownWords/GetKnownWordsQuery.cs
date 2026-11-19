namespace ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetKnownWords;

/// <summary>Known vocabulary words of a topic, alphabetical.</summary>
public sealed record GetKnownWordsQuery(Guid LearnerId, string Topic) : IRequest<IReadOnlyList<string>>;
