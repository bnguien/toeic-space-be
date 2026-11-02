namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetPublicLessonPreview;

public sealed class GetPublicLessonPreviewValidator : AbstractValidator<GetPublicLessonPreviewQuery>
{
    public GetPublicLessonPreviewValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
    }
}
