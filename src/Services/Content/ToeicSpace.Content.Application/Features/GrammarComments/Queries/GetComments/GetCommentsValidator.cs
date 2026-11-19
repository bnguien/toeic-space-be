namespace ToeicSpace.Content.Application.Features.GrammarComments.Queries.GetComments;

public sealed class GetCommentsValidator : AbstractValidator<GetCommentsQuery>
{
    public GetCommentsValidator()
    {
        RuleFor(x => x.Target).Must(t => GrammarCommentRules.Target().IsMatch(t ?? string.Empty)).WithMessage("Invalid target.");
        RuleFor(x => x.Before).GreaterThan(0).When(x => x.Before is not null);
    }
}
