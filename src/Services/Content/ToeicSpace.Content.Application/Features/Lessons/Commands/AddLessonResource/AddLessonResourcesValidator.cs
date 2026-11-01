namespace ToeicSpace.Content.Application.Features.Lessons.Commands.AddLessonResource;

public sealed class AddLessonResourcesValidator : AbstractValidator<AddLessonResourcesCommand>
{
    public AddLessonResourcesValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.ObjectKeys).NotNull().Must(keys => keys is { Count: >= 1 and <= 20 })
            .WithMessage("Provide between 1 and 20 attachment keys.");
        RuleFor(x => x.ObjectKeys).Must(keys => keys.Distinct(StringComparer.Ordinal).Count() == keys.Count)
            .WithMessage("Attachment keys must be unique.");
    }
}
