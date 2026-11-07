using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationConfig;

public sealed class GetDictationConfigHandler(IDictationCatalogRepository catalog)
    : IRequestHandler<GetDictationConfigQuery, DictationConfigDto>
{
    private static readonly DictationConfigDto Config = new(
        DictationCatalog.Levels.Select(x => x.ToDto()).ToArray(),
        DictationCatalog.Topics.Select(x => new DictationTopicDto(x.Key, x.Name)).ToArray(),
        SpeechVoices.All.Select(x => x.ToDto()).ToArray(),
        SpeechVoices.DefaultKey, SpeechVoices.RateMin, SpeechVoices.RateMax, DictationRules.PassScore,
        DictationRules.PassThreshold,
        new DictationRulesDto(DictationRules.CleanPassLevel, DictationRules.SupportGatedLevel,
            DictationRules.LevelOneListHideStreak, DictationRules.AssistedRateWarning,
            DictationRules.SuggestLevelUpStreak), []);

    public async Task<DictationConfigDto> Handle(GetDictationConfigQuery request, CancellationToken ct) =>
        Config with { Packs = await catalog.GetPackSummariesAsync(ct) };
}
