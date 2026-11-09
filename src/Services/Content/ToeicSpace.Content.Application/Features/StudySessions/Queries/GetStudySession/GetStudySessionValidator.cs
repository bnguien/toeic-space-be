using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.StudySessions.Queries.GetStudySession;

public sealed class GetStudySessionValidator : AbstractValidator<GetStudySessionQuery>
{
    public GetStudySessionValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
