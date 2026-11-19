using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoExercises.Queries.CheckCloze;

public sealed class CheckClozeValidator : AbstractValidator<CheckClozeQuery>
{
    public CheckClozeValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.VideoId).Must(VideoLinks.IsValidVideoId).WithMessage("Invalid video id.");
        RuleFor(x => x.Lang).Must(VideoLinks.IsValidLang).WithMessage("lang is required.");
        RuleFor(x => x.Cue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Answers).NotNull().Must(a => a is { Count: > 0 and <= VideoExerciseGrader.MaxAnswersPerCheck })
            .WithMessage($"answers must hold 1..{VideoExerciseGrader.MaxAnswersPerCheck} blanks.");
    }
}
