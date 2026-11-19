using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarContent.Queries.GetGrammarTopic;

/// <summary>Knowledge, vocabulary and exercises of a topic ("01".."44") without answers.</summary>
public sealed record GetGrammarTopicQuery(string Topic) : IRequest<GrammarTopicPayload>;
