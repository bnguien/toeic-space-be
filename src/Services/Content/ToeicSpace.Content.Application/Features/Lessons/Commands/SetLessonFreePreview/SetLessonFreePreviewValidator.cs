namespace ToeicSpace.Content.Application.Features.Lessons.Commands.SetLessonFreePreview;

public sealed class SetLessonFreePreviewValidator : AbstractValidator<SetLessonFreePreviewCommand>
{
    public SetLessonFreePreviewValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
    }
}
