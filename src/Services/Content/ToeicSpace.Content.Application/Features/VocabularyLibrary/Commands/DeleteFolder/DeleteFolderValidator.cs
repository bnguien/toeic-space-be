namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.DeleteFolder;

public sealed class DeleteFolderValidator : AbstractValidator<DeleteFolderCommand>
{
    public DeleteFolderValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.FolderId).NotEmpty();
    }
}
