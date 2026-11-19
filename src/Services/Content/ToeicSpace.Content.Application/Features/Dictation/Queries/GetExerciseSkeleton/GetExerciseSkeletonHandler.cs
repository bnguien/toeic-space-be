using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Models.Dictation;
using ToeicSpace.Content.Application.Models.Speech;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetExerciseSkeleton;

/// <summary>
/// No answer letters: slot counts, punctuation, meaning, keywords (level 2+), IPA, grammar notes and one-way word
/// proofs so the client can colour words while typing. Words, proofs and hint indexes share one token list.
/// </summary>
public sealed class GetExerciseSkeletonHandler(IDictationCatalogRepository catalog, DictationProofService proofs,
    ISpeechPrewarmQueue prewarm, DictationSettings settings)
    : IRequestHandler<GetExerciseSkeletonQuery, DictationSkeletonDto>
{
    private const int MaxPrewarm = 8;

    public async Task<DictationSkeletonDto> Handle(GetExerciseSkeletonQuery request, CancellationToken ct)
    {
        var item = await catalog.GetItemAsync(request.ItemId, ct) ?? throw DictationErrors.ItemNotFound();
        await PrewarmNextAsync(item, request, ct);
        var words = DictationGrader.Skeleton(item.Text);
        return new DictationSkeletonDto(words.Select(x => x.ToDto()).ToArray(), words.Count, item.Translation,
            item.Level, item.Level < 2 ? [] : item.Keywords.Select(x => x.ToDto()).ToArray(),
            proofs.For(item).ToDto(), item.Ipa, item.Notes.Select(x => x.ToDto()).ToArray());
    }

    private async Task PrewarmNextAsync(DictationItem item, GetExerciseSkeletonQuery request, CancellationToken ct)
    {
        var ahead = Math.Clamp(settings.PrewarmAhead, 0, MaxPrewarm);
        if (ahead == 0) return;
        var voice = SpeechVoices.Resolve(string.IsNullOrWhiteSpace(request.Voice) ? item.VoiceKey : request.Voice);
        var rate = SpeechVoices.ClampRate(item.Rate);
        var ids = (request.Next ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0).Where(x => x > 0).Distinct().Take(ahead).ToArray();
        var next = ids.Length > 0
            ? await catalog.GetItemsAsync(ids, ct)
            : await catalog.GetFollowingItemsAsync(item, ahead, ct);
        prewarm.Enqueue(next.Select(x => new SpeechJob(x.Text, voice.Key, rate)));
    }
}
