using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.AiFillWord;

public sealed class AiFillWordValidator : AbstractValidator<AiFillWordCommand>
{
    public AiFillWordValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.SavedWordId).NotEmpty();
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
