namespace ToeicSpace.Content.Application.Features.Courses.Commands.CreateCourse;

public sealed class CreateCourseValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseValidator()
    {
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(160);
        RuleFor(x => x.ShortDescription).MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.ExamType).IsInEnum();
        RuleFor(x => x.Level).IsInEnum();
    }
}
