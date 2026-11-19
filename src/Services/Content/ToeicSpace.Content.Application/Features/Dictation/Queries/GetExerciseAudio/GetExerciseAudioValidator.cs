namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetExerciseAudio;

public sealed class GetExerciseAudioValidator : AbstractValidator<GetExerciseAudioQuery>
{
    public GetExerciseAudioValidator()
    {
        RuleFor(x => x.ItemId).GreaterThan(0);
        RuleFor(x => x.Voice).MaximumLength(30);
    }
}
