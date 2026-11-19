using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.StudySessions.Queries.GetActiveStudySession;

public sealed class GetActiveStudySessionValidator : AbstractValidator<GetActiveStudySessionQuery>
{
    public GetActiveStudySessionValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Mode).IsInEnum().When(x => x.Mode.HasValue);
        RuleFor(x => x.SourceCount).LessThanOrEqualTo(1)
            .WithMessage("Use at most one of setId, folderId, library=true, due=true or mistakes=true.");
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
