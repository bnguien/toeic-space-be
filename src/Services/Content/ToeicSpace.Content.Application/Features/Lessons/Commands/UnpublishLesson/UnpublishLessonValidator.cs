namespace ToeicSpace.Content.Application.Features.Lessons.Commands.UnpublishLesson;

public sealed class UnpublishLessonValidator : AbstractValidator<UnpublishLessonCommand>
{
    public UnpublishLessonValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
    }
}
