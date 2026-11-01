namespace ToeicSpace.Content.Application.Features.Lessons.Commands.ConfirmLessonUpload;

public sealed class ConfirmLessonUploadValidator : AbstractValidator<ConfirmLessonUploadCommand>
{
    public ConfirmLessonUploadValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminId).NotEmpty();
    }
}

public sealed class ConfirmLessonUploadsValidator : AbstractValidator<ConfirmLessonUploadsCommand>
{
    public ConfirmLessonUploadsValidator()
    {
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.Keys).NotNull().Must(keys => keys is { Count: >= 1 and <= 20 })
            .WithMessage("Provide between 1 and 20 media keys per confirmation batch.");
        RuleForEach(x => x.Keys).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Keys).Must(keys => keys.Distinct(StringComparer.Ordinal).Count() == keys.Count)
            .WithMessage("Media keys must be unique.");
    }
}
