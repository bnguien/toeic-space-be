namespace ToeicSpace.Content.Application.Features.Courses.Commands.ConfirmThumbnail;

public sealed class ConfirmThumbnailValidator : AbstractValidator<ConfirmThumbnailCommand>
{
    public ConfirmThumbnailValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(160);
        RuleFor(x => x.AdminId).NotEmpty();
    }
}

public sealed class ConfirmThumbnailsValidator : AbstractValidator<ConfirmThumbnailsCommand>
{
    public ConfirmThumbnailsValidator()
    {
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.Keys).NotNull().Must(x => x is { Count: >= 1 and <= 20 })
            .WithMessage("Provide between 1 and 20 thumbnail keys.");
        RuleForEach(x => x.Keys).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Keys).Must(x => x.Distinct(StringComparer.Ordinal).Count() == x.Count)
            .WithMessage("Thumbnail keys must be unique.");
    }
}
