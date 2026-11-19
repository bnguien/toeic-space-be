namespace ToeicSpace.Content.Application.Features.VideoWatches.Commands.SetVideoPublic;

public sealed class SetVideoPublicValidator : AbstractValidator<SetVideoPublicCommand>
{
    public SetVideoPublicValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).MaximumLength(200);
        RuleFor(x => x.AvatarUrl).MaximumLength(1000);
    }
}
