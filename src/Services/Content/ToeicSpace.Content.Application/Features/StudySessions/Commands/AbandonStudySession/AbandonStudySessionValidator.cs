namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.AbandonStudySession;

public sealed class AbandonStudySessionValidator : AbstractValidator<AbandonStudySessionCommand>
{
    public AbandonStudySessionValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
    }
}
