using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.DeleteFolder;

public sealed class DeleteFolderHandler(ILearnerLibraryRepository library, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<DeleteFolderHandler> logger) : IRequestHandler<DeleteFolderCommand>
{
    public async Task Handle(DeleteFolderCommand request, CancellationToken ct)
    {
        var folder = await library.GetFolderForUpdateAsync(request.LearnerId, request.FolderId, ct)
            ?? throw VocabularyErrors.FolderNotFound();
        var now = clock.GetUtcNow().UtcDateTime;
        var words = await library.GetSavedWordsForUpdateAsync(request.LearnerId, folder.Id, ct);
        foreach (var word in words) { word.FolderId = null; word.UpdatedAt = now; }
        library.RemoveFolder(folder);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Learner {LearnerId} deleted vocabulary folder {FolderId}; {Count} words unfiled",
            request.LearnerId, folder.Id, words.Count);
    }
}
