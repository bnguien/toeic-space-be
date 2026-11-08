using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Services.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarContent.Queries.GetGrammarTopic;

public sealed class GetGrammarTopicHandler(GrammarContentProvider content)
    : IRequestHandler<GetGrammarTopicQuery, GrammarTopicPayload>
{
    public async Task<GrammarTopicPayload> Handle(GetGrammarTopicQuery request, CancellationToken ct)
    {
        var topic = await content.GetTopicAsync(request.Topic, ct);
        return new GrammarTopicPayload(topic.PublicJson, topic.Version);
    }
}
