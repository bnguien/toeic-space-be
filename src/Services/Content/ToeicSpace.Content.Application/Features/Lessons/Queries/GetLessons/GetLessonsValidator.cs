namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessons;

public sealed class GetLessonsValidator : AbstractValidator<GetLessonsQuery>
{
    public GetLessonsValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
    }
}
