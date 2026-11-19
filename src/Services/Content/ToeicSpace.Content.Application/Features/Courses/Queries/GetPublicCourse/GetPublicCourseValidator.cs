namespace ToeicSpace.Content.Application.Features.Courses.Queries.GetPublicCourse;

public sealed class GetPublicCourseValidator : AbstractValidator<GetPublicCourseQuery>
{
    public GetPublicCourseValidator() => RuleFor(x => x.Id).NotEmpty();
}
