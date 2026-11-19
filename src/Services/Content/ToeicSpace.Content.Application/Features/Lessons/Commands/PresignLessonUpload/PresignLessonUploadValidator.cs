namespace ToeicSpace.Content.Application.Features.Lessons.Commands.PresignLessonUpload;

public sealed class PresignLessonUploadValidator : AbstractValidator<PresignLessonUploadCommand>
{
    public PresignLessonUploadValidator()
    {
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty();
        RuleFor(x => x.SizeBytes).GreaterThan(0);
    }
}

public sealed class PresignLessonUploadsValidator : AbstractValidator<PresignLessonUploadsCommand>
{
    public PresignLessonUploadsValidator()
    {
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.Files).NotNull().Must(files => files is { Count: >= 1 and <= 20 })
            .WithMessage("Provide between 1 and 20 files per upload batch.");
        RuleForEach(x => x.Files).ChildRules(file =>
        {
            file.RuleFor(x => x.Kind).IsInEnum();
            file.RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
            file.RuleFor(x => x.ContentType).NotEmpty();
            file.RuleFor(x => x.SizeBytes).GreaterThan(0);
        });
    }
}
