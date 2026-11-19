namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessonDetail;

public sealed class GetLessonDetailValidator : AbstractValidator<GetLessonDetailQuery>
{
    public GetLessonDetailValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
    }
}
