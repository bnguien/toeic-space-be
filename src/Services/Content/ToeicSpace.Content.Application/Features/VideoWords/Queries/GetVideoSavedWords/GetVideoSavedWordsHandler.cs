using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.VideoWords.Queries.GetVideoSavedWords;

public sealed class GetVideoSavedWordsHandler(IVideoLexiconRepository lexicon)
    : IRequestHandler<GetVideoSavedWordsQuery, VideoSavedWordsDto>
{
    public const int MaxRows = 2000;

    public async Task<VideoSavedWordsDto> Handle(GetVideoSavedWordsQuery request, CancellationToken ct)
    {
        var rows = await lexicon.GetSavedWordsAsync(request.LearnerId, MaxRows, ct);
        var total = rows.Count < MaxRows ? rows.Count : await lexicon.CountSavedWordsAsync(request.LearnerId, ct);
        return new VideoSavedWordsDto(total, rows.Select(x => x.ToVideoDto()).ToList());
    }
}
