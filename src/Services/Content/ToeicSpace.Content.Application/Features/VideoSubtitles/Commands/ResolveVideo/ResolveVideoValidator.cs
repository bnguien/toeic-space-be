namespace ToeicSpace.Content.Application.Features.VideoSubtitles.Commands.ResolveVideo;

public sealed class ResolveVideoValidator : AbstractValidator<ResolveVideoCommand>
{
    public ResolveVideoValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Url).NotEmpty().MaximumLength(ResolveVideoCommand.MaxUrlLength);
    }
}
