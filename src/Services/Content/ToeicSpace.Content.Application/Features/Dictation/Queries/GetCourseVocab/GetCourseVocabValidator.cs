namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetCourseVocab;

public sealed class GetCourseVocabValidator : AbstractValidator<GetCourseVocabQuery>
{
    public GetCourseVocabValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
    }
}
