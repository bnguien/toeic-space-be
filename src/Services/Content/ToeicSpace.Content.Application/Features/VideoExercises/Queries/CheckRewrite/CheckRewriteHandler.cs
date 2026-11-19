using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Services.Video;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoExercises.Queries.CheckRewrite;

public sealed class CheckRewriteHandler(VideoExerciseCues cues) : IRequestHandler<CheckRewriteQuery, RewriteCheckResultDto>
{
    public async Task<RewriteCheckResultDto> Handle(CheckRewriteQuery request, CancellationToken ct)
    {
        var sentence = await cues.SentenceAsync(request.LearnerId, request.VideoId, request.Lang, request.Cue, ct);
        return new RewriteCheckResultDto(VideoExerciseGrader.GradeRewrite(sentence, request.Words));
    }
}
