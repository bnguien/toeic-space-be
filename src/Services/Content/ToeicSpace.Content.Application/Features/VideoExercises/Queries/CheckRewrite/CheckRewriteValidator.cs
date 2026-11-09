using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoExercises.Queries.CheckRewrite;

public sealed class CheckRewriteValidator : AbstractValidator<CheckRewriteQuery>
{
    public CheckRewriteValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.VideoId).Must(VideoLinks.IsValidVideoId).WithMessage("Invalid video id.");
        RuleFor(x => x.Lang).Must(VideoLinks.IsValidLang).WithMessage("lang is required.");
        RuleFor(x => x.Cue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Words).NotNull().Must(w => w is { Count: <= VideoExerciseGrader.MaxWordsPerSentence })
            .WithMessage($"words must hold at most {VideoExerciseGrader.MaxWordsPerSentence} words.");
    }
}
