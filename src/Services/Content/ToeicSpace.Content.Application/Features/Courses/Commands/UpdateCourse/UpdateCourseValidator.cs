namespace ToeicSpace.Content.Application.Features.Courses.Commands.UpdateCourse;

public sealed class UpdateCourseValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(160);
        RuleFor(x => x.ShortDescription).MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.ExamType).IsInEnum();
        RuleFor(x => x.Level).IsInEnum();
    }
}
