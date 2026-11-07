using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.CompleteStudySession;

public sealed class CompleteStudySessionValidator : AbstractValidator<CompleteStudySessionCommand>
{
    public CompleteStudySessionValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
