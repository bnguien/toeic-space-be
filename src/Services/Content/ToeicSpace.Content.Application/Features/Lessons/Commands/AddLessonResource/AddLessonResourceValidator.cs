namespace ToeicSpace.Content.Application.Features.Lessons.Commands.AddLessonResource;

public sealed class AddLessonResourceValidator : AbstractValidator<AddLessonResourceCommand>
{
    public AddLessonResourceValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.ObjectKey).NotEmpty().MaximumLength(200);
    }
}
