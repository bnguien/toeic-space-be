namespace ToeicSpace.Content.Application.Features.Courses.Queries.GetCourseById;

public sealed class GetCourseByIdValidator : AbstractValidator<GetCourseByIdQuery>
{
    public GetCourseByIdValidator() => RuleFor(x => x.CourseId).NotEmpty();
}
