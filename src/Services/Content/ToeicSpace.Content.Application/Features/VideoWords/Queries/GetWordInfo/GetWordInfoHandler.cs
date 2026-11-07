using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Services.Video;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VideoWords.Queries.GetWordInfo;

public sealed class GetWordInfoHandler(WordDetailService details) : IRequestHandler<GetWordInfoQuery, WordInfoDto>
{
    public async Task<WordInfoDto> Handle(GetWordInfoQuery request, CancellationToken ct) =>
        await details.LookupAsync(request.Word, ct) ?? throw VideoErrors.InvalidWord();
}
