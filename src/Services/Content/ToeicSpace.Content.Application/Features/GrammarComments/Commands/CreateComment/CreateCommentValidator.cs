using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Commands.CreateComment;

public sealed class CreateCommentValidator : AbstractValidator<CreateCommentCommand>
{
    public CreateCommentValidator()
    {
        RuleFor(x => x.Target).Must(t => GrammarCommentRules.Target().IsMatch(t ?? string.Empty)).WithMessage("Invalid target.");
        RuleFor(x => x.Content).NotNull().MaximumLength(GrammarComment.MaxLength * 2);
        RuleFor(x => x.ParentId).GreaterThan(0).When(x => x.ParentId is not null);
    }
}
