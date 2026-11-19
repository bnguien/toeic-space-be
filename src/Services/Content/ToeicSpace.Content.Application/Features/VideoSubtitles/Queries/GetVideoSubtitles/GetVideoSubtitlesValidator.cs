using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoSubtitles.Queries.GetVideoSubtitles;

public sealed class GetVideoSubtitlesValidator : AbstractValidator<GetVideoSubtitlesQuery>
{
    public GetVideoSubtitlesValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.VideoId).Must(VideoLinks.IsValidVideoId).WithMessage("Invalid video id.");
        RuleFor(x => x.LangList).NotEmpty().WithMessage("langs is required (for example ?langs=en,vi).");
    }
}
