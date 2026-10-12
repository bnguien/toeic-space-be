namespace ToeicSpace.Identity.Application.Features.Profile.Commands;

public class UploadAvatarValidator : AbstractValidator<UploadAvatarCommand>
{
    public UploadAvatarValidator()
    {
        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.")
            .Must(BeAValidImageContentType).WithMessage("Only image/jpeg, image/png, or image/webp are allowed.");
    }

    private bool BeAValidImageContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var type = contentType.ToLowerInvariant().Trim();
        return type is "image/jpeg" or "image/png" or "image/webp";
    }
}