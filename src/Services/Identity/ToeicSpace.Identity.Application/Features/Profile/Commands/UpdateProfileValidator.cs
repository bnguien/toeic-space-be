using ToeicSpace.Identity.Application.Features.Register.Commands;

namespace ToeicSpace.Identity.Application.Features.Profile.Commands;

public sealed class UpdateProfileValidator :
    AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Phone)
            .Must(IdentityNormalizer.IsValidPhone)
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Date of birth must be in the past.")
            .When(x => x.DateOfBirth.HasValue);
        
        RuleFor(x => x.Biography)
            .MaximumLength(300).WithMessage("Biography must not exceed 300 characters.");
        
        RuleFor(x => x.TargetScore)
            .InclusiveBetween(10, 990).WithMessage("Target score must be between 10 and 990.");
        
        RuleFor(x => x.AvatarUrl)
            .MaximumLength(500).WithMessage("Avatar url must not exceed 500 characters.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var outUri) 
            && (outUri.Scheme == Uri.UriSchemeHttp || outUri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Avatar URL must be a valid HTTP or HTTPS web address.")
            .When(x => !string.IsNullOrEmpty(x.AvatarUrl));
    }
}