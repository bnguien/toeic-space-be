using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Grammar;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Services.Grammar;

/// <summary>Scoped access to the cached grammar content; 503 <c>grammar_content_unavailable</c> before the first import.</summary>
public sealed class GrammarContentProvider(GrammarContentCache cache, IGrammarContentRepository repository)
{
    public async Task<GrammarContentBundle> GetAsync(CancellationToken ct) =>
        await cache.GetAsync(repository.ListAsync, ct) ?? throw GrammarErrors.ContentUnavailable();

    public async Task<GrammarTopicContent> GetTopicAsync(string topic, CancellationToken ct) =>
        (await GetAsync(ct)).Topics.GetValueOrDefault(topic) ?? throw GrammarErrors.TopicNotFound();

    public async Task<GrammarStageContent> GetStageAsync(string stage, CancellationToken ct) =>
        (await GetAsync(ct)).Stages.GetValueOrDefault(stage) ?? throw GrammarErrors.StageNotFound();
}
