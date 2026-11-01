namespace ToeicSpace.Content.Application.Features.Courses.Commands.PublishCourse;

public sealed class PublishCourseValidator : AbstractValidator<PublishCourseCommand>
{
    public PublishCourseValidator() => RuleFor(x => x.CourseId).NotEmpty();
}
