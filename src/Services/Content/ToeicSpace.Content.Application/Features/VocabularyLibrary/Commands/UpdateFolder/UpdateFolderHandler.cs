using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateFolder;

public sealed class UpdateFolderHandler(ILearnerLibraryRepository library, ILearnerProgressRepository progress,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<UpdateFolderCommand, FolderDto>
{
    public async Task<FolderDto> Handle(UpdateFolderCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var folder = await library.GetFolderForUpdateAsync(request.LearnerId, request.FolderId, ct)
            ?? throw VocabularyErrors.FolderNotFound();
        if (request.Name is not null)
        {
            folder.Rename(request.Name, now);
            if (await library.FolderNameExistsAsync(request.LearnerId, folder.NormalizedName, folder.Id, ct))
                throw VocabularyErrors.FolderNameTaken();
        }
        if (request.Order is { } order) { folder.Order = order; folder.UpdatedAt = now; }
        await unitOfWork.SaveChangesAsync(ct);

        var today = SpacedRepetitionScheduler.LocalDay(now, request.TzOffset);
        var words = (await library.GetSavedWordsAsync(request.LearnerId, ct)).Where(x => x.FolderId == folder.Id).ToArray();
        var rows = (await progress.GetForWordsAsync(request.LearnerId, words.Select(x => x.WordId).ToArray(), ct))
            .ToDictionary(x => x.WordId);
        return folder.ToDto(LibraryCounter.Count(words, rows, today));
    }
}
