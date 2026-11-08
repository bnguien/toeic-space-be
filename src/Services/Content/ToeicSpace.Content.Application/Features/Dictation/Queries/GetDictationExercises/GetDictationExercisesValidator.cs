namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationExercises;

/// <summary>Out-of-range levels fall back to level 1 like Studychill; the course id must be positive when sent.</summary>
public sealed class GetDictationExercisesValidator : AbstractValidator<GetDictationExercisesQuery>
{
    public GetDictationExercisesValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0).When(x => x.CourseId is not null);
    }
}
