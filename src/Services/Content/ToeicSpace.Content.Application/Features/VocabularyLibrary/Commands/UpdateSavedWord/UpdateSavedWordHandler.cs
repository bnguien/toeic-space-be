using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateSavedWord;

public sealed class UpdateSavedWordHandler(ILearnerLibraryRepository library, LibraryWordService words,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<UpdateSavedWordCommand, SavedWordDto>
{
    public async Task<SavedWordDto> Handle(UpdateSavedWordCommand request, CancellationToken ct)
    {
        var saved = await library.GetSavedWordForUpdateAsync(request.LearnerId, request.SavedWordId, ct)
            ?? throw VocabularyErrors.SavedWordNotFound();
        var now = clock.GetUtcNow().UtcDateTime;
        if (request.Custom is not null)
        {
            if (!saved.Word!.IsCustom) throw VocabularyErrors.WordNotCustom();
            await words.ApplyCustomAsync(saved.Word, request.LearnerId, request.Custom, now, ct);
        }
        if (request.FolderId.IsSet)
        {
            await words.EnsureFolderAsync(request.LearnerId, request.FolderId.Value, ct);
            saved.FolderId = request.FolderId.Value;
        }
        if (request.Note.IsSet) saved.Note = LibraryWordService.Clean(request.Note.Value);
        saved.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(ct);
        return await words.ToDtoAsync(saved, request.TzOffset, ct);
    }
}
