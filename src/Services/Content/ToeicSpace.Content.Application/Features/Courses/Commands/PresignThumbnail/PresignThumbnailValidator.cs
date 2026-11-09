namespace ToeicSpace.Content.Application.Features.Courses.Commands.PresignThumbnail;

public sealed class PresignThumbnailValidator : AbstractValidator<PresignThumbnailCommand>
{
    public PresignThumbnailValidator()
    {
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.ContentType).Must(x => x is "image/jpeg" or "image/png" or "image/webp")
            .WithMessage("Only JPEG, PNG and WebP thumbnails are supported.");
        RuleFor(x => x.SizeBytes).InclusiveBetween(1, 5 * 1024 * 1024);
    }
}
