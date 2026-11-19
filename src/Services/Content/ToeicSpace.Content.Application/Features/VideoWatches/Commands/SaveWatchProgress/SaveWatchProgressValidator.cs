using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoWatches.Commands.SaveWatchProgress;

public sealed class SaveWatchProgressValidator : AbstractValidator<SaveWatchProgressCommand>
{
    public SaveWatchProgressValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.VideoId).Must(VideoLinks.IsValidVideoId).WithMessage("Invalid video id.");
        RuleFor(x => x.Position).Must(double.IsFinite);
        RuleFor(x => x.Duration).Must(double.IsFinite);
    }
}
