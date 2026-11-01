using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.StartStudySession;

public sealed class StartStudySessionValidator : AbstractValidator<StartStudySessionCommand>
{
    public StartStudySessionValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.SourceCount).Equal(1)
            .WithMessage("Specify exactly one of setId, folderId, library=true, due=true or wordIds.");
        RuleFor(x => x.SetId).NotEqual(Guid.Empty).When(x => x.SetId.HasValue);
        RuleFor(x => x.FolderId).NotEqual(Guid.Empty).When(x => x.FolderId.HasValue);
        RuleFor(x => x.WordIds!.Count).InclusiveBetween(1, StartStudySessionCommand.MaxWords)
            .WithMessage($"wordIds must contain 1 to {StartStudySessionCommand.MaxWords} ids.").When(x => x.WordIds is not null);
        RuleForEach(x => x.WordIds).NotEmpty().When(x => x.WordIds is not null);
        RuleFor(x => x.Limit).InclusiveBetween(1, StartStudySessionCommand.MaxLimit);
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
