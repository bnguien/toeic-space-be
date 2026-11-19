using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Services.Video;

namespace ToeicSpace.Content.Application.Features.VideoWords.Queries.GetWordMeanings;

public sealed class GetWordMeaningsHandler(WordDetailService details) : IRequestHandler<GetWordMeaningsQuery, WordMeaningsDto>
{
    public async Task<WordMeaningsDto> Handle(GetWordMeaningsQuery request, CancellationToken ct) =>
        new(await details.QuickMeaningsAsync(request.WordList, ct));
}
