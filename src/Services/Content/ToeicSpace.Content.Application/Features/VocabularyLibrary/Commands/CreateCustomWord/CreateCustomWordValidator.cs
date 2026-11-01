using ToeicSpace.Content.Application.Common.Validation;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateCustomWord;

public sealed class CreateCustomWordValidator : AbstractValidator<CreateCustomWordCommand>
{
    public CreateCustomWordValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.ToInput()).SetValidator(new CustomWordInputValidator()).OverridePropertyName("word");
        RuleFor(x => x.FolderId).NotEqual(Guid.Empty).When(x => x.FolderId.HasValue);
        RuleFor(x => x.Note).MaximumLength(SavedWord.NoteMaxLength);
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
