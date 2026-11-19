namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.RemoveSavedWord;

public sealed class RemoveSavedWordValidator : AbstractValidator<RemoveSavedWordCommand>
{
    public RemoveSavedWordValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.SavedWordId).NotEmpty();
    }
}
