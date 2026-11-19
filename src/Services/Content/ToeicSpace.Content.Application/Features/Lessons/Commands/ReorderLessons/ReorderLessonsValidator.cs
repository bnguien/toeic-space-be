namespace ToeicSpace.Content.Application.Features.Lessons.Commands.ReorderLessons;

public sealed class ReorderLessonsValidator : AbstractValidator<ReorderLessonsCommand>
{
    public ReorderLessonsValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.LessonIds).NotNull().Must(ids => ids is not null && ids.All(x => x != Guid.Empty)
            && ids.Distinct().Count() == ids.Count).WithMessage("Lesson IDs must be non-empty and unique.");
    }
}
