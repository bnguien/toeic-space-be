namespace ToeicSpace.Content.Application.Features.Lessons.Commands.RemoveLessonResource;

public sealed class RemoveLessonResourceValidator : AbstractValidator<RemoveLessonResourceCommand>
{
    public RemoveLessonResourceValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty();
    }
}
