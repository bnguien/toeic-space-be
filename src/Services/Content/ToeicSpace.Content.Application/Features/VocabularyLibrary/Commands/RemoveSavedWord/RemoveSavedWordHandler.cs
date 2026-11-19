using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.RemoveSavedWord;

public sealed class RemoveSavedWordHandler(ILearnerLibraryRepository library, ILearnerProgressRepository progress,
    IUnitOfWork unitOfWork, ILogger<RemoveSavedWordHandler> logger) : IRequestHandler<RemoveSavedWordCommand>
{
    public async Task Handle(RemoveSavedWordCommand request, CancellationToken ct)
    {
        var saved = await library.GetSavedWordForUpdateAsync(request.LearnerId, request.SavedWordId, ct)
            ?? throw VocabularyErrors.SavedWordNotFound();
        var word = saved.Word!;
        library.RemoveSavedWord(saved);
        if (word.IsCustom)
        {
            await progress.RemoveAllForWordAsync(word.Id, ct);
            library.RemoveWord(word);
        }
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Learner {LearnerId} removed saved word {SavedWordId} (custom: {IsCustom})",
            request.LearnerId, saved.Id, word.IsCustom);
    }
}
