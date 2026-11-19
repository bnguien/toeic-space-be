using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateFolder;

public sealed class CreateFolderHandler(ILearnerLibraryRepository library, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<CreateFolderHandler> logger) : IRequestHandler<CreateFolderCommand, FolderDto>
{
    public async Task<FolderDto> Handle(CreateFolderCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var folder = new VocabularyFolder { LearnerId = request.LearnerId, CreatedAt = now };
        folder.Rename(request.Name, now);
        if (await library.FolderNameExistsAsync(request.LearnerId, folder.NormalizedName, null, ct))
            throw VocabularyErrors.FolderNameTaken();
        folder.Order = await library.GetMaxFolderOrderAsync(request.LearnerId, ct) + 1;
        library.AddFolder(folder);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Learner {LearnerId} created vocabulary folder {FolderId}", request.LearnerId, folder.Id);
        return folder.ToDto(new LibraryCountsDto(0, 0, 0, 0));
    }
}
