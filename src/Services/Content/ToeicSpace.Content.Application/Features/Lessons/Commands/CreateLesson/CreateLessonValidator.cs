namespace ToeicSpace.Content.Application.Features.Lessons.Commands.CreateLesson;

public sealed class CreateLessonValidator : AbstractValidator<CreateLessonCommand>
{
    public CreateLessonValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.TextContent).MaximumLength(100000);
        RuleFor(x => x.SortOrder).GreaterThan(0).When(x => x.SortOrder.HasValue);
        RuleFor(x => x.VideoKeys).Must(keys => keys is null || keys.Count <= 10)
            .WithMessage("A lesson can contain at most 10 videos.");
        RuleFor(x => x.VideoKeys).Must(keys => keys is null || keys.Distinct(StringComparer.Ordinal).Count() == keys.Count)
            .WithMessage("Video keys must be unique.");
    }
}
