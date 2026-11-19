using ToeicSpace.Content.Application.Common.Validation;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateSavedWord;

public sealed class UpdateSavedWordValidator : AbstractValidator<UpdateSavedWordCommand>
{
    public UpdateSavedWordValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.SavedWordId).NotEmpty();
        RuleFor(x => x.FolderId.Value).NotEqual(Guid.Empty).WithName("folderId")
            .When(x => x.FolderId.IsSet && x.FolderId.Value.HasValue);
        RuleFor(x => x.Note.Value).MaximumLength(SavedWord.NoteMaxLength).WithName("note").When(x => x.Note.IsSet);
        RuleFor(x => x.Custom!).SetValidator(new CustomWordInputValidator()).When(x => x.Custom is not null);
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
