using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.AnswerStudySession;

public sealed class AnswerStudySessionValidator : AbstractValidator<AnswerStudySessionCommand>
{
    public AnswerStudySessionValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.WordId).NotEmpty();
        RuleFor(x => x.ClientEventId).NotEmpty();
        RuleFor(x => x.Grade).IsInEnum();
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
