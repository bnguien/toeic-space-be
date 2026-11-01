namespace ToeicSpace.Content.Application.Features.Lessons.Commands.DeleteLesson;

public sealed class DeleteLessonValidator : AbstractValidator<DeleteLessonCommand>
{
    public DeleteLessonValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
    }
}
