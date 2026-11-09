namespace ToeicSpace.Content.Application.Features.Courses.Commands.RestoreCourse;

public sealed class RestoreCourseValidator : AbstractValidator<RestoreCourseCommand>
{
    public RestoreCourseValidator() => RuleFor(x => x.CourseId).NotEmpty();
}
