namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetExerciseSkeleton;

public sealed class GetExerciseSkeletonValidator : AbstractValidator<GetExerciseSkeletonQuery>
{
    public GetExerciseSkeletonValidator()
    {
        RuleFor(x => x.ItemId).GreaterThan(0);
        RuleFor(x => x.Voice).MaximumLength(30);
        RuleFor(x => x.Next).MaximumLength(200);
    }
}
