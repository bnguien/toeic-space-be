using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Commands.ReportUnplayable;

public sealed class ReportUnplayableValidator : AbstractValidator<ReportUnplayableCommand>
{
    public ReportUnplayableValidator()
    {
        RuleFor(x => x.VideoId).Must(VideoLinks.IsEmbedVideoId).WithMessage("videoId must be an 11-character YouTube id.");
    }
}
