using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Services.Video;

namespace ToeicSpace.Content.Application.Features.VideoWords.Queries.GlossWords;

public sealed class GlossWordsHandler(VideoGlossService gloss) : IRequestHandler<GlossWordsQuery, GlossResultDto>
{
    public Task<GlossResultDto> Handle(GlossWordsQuery request, CancellationToken ct) =>
        gloss.GlossAsync(request.Words, request.Level, request.SignedIn, ct);
}
