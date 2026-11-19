using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.SaveWord;

public sealed class SaveWordHandler(ILearnerLibraryRepository library, IVocabularyCatalogRepository catalog,
    LibraryWordService words, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<SaveWordHandler> logger)
    : IRequestHandler<SaveWordCommand, SavedWordUpsertResult>
{
    public async Task<SavedWordUpsertResult> Handle(SaveWordCommand request, CancellationToken ct)
    {
        if (!await catalog.IsWordAccessibleAsync(request.WordId, request.LearnerId, ct)) throw VocabularyErrors.WordNotFound();
        if (request.FolderId.IsSet) await words.EnsureFolderAsync(request.LearnerId, request.FolderId.Value, ct);

        var now = clock.GetUtcNow().UtcDateTime;
        var saved = await library.GetSavedWordByWordForUpdateAsync(request.LearnerId, request.WordId, ct);
        var created = saved is null;
        if (saved is null)
        {
            saved = new SavedWord { LearnerId = request.LearnerId, WordId = request.WordId, SavedAt = now };
            library.AddSavedWord(saved);
        }
        if (request.FolderId.IsSet) saved.FolderId = request.FolderId.Value;
        if (request.Note.IsSet) saved.Note = LibraryWordService.Clean(request.Note.Value);
        saved.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(ct);
        if (created) logger.LogInformation("Learner {LearnerId} saved word {WordId}", request.LearnerId, request.WordId);

        var stored = await library.GetSavedWordForUpdateAsync(request.LearnerId, saved.Id, ct);
        return new SavedWordUpsertResult(await words.ToDtoAsync(stored!, request.TzOffset, ct), created);
    }
}
