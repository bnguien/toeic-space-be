namespace ToeicSpace.Content.Application.Features.Lessons.Commands.PublishLesson;

public sealed class PublishLessonValidator : AbstractValidator<PublishLessonCommand>
{
    public PublishLessonValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
    }
}
