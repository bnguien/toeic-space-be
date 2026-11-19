using ToeicSpace.Content.Application.Common.Validation;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateFolder;

public sealed class UpdateFolderValidator : AbstractValidator<UpdateFolderCommand>
{
    public UpdateFolderValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.FolderId).NotEmpty();
        RuleFor(x => x).Must(x => x.Name is not null || x.Order.HasValue)
            .WithMessage("Provide name and/or order.");
        RuleFor(x => x.Name).Must(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length <= VocabularyFolder.NameMaxLength)
            .WithMessage($"name must be 1 to {VocabularyFolder.NameMaxLength} characters.").When(x => x.Name is not null);
        RuleFor(x => x.Order).InclusiveBetween(1, 10_000).When(x => x.Order.HasValue);
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
