namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessonPreview;

public sealed class GetLessonPreviewValidator : AbstractValidator<GetLessonPreviewQuery>
{
    public GetLessonPreviewValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
    }
}
