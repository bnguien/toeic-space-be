using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Services.Video;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoExercises.Queries.CheckCloze;

public sealed class CheckClozeHandler(VideoExerciseCues cues) : IRequestHandler<CheckClozeQuery, ClozeCheckResultDto>
{
    public async Task<ClozeCheckResultDto> Handle(CheckClozeQuery request, CancellationToken ct)
    {
        var sentence = await cues.SentenceAsync(request.LearnerId, request.VideoId, request.Lang, request.Cue, ct);
        return new ClozeCheckResultDto(VideoExerciseGrader.GradeCloze(sentence, request.Answers));
    }
}
