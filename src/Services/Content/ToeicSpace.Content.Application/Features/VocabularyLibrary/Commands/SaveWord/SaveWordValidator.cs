using ToeicSpace.Content.Application.Common.Validation;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.SaveWord;

public sealed class SaveWordValidator : AbstractValidator<SaveWordCommand>
{
    public SaveWordValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.WordId).NotEmpty();
        RuleFor(x => x.FolderId.Value).NotEqual(Guid.Empty).WithName("folderId")
            .When(x => x.FolderId.IsSet && x.FolderId.Value.HasValue);
        RuleFor(x => x.Note.Value).MaximumLength(SavedWord.NoteMaxLength).WithName("note").When(x => x.Note.IsSet);
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
