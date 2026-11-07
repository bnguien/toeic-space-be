using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateFolder;

public sealed class CreateFolderValidator : AbstractValidator<CreateFolderCommand>
{
    public CreateFolderValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Name).Must(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length <= VocabularyFolder.NameMaxLength)
            .WithMessage($"name must be 1 to {VocabularyFolder.NameMaxLength} characters.");
    }
}
