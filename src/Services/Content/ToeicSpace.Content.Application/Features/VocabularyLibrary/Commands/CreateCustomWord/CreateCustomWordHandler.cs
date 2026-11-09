using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateCustomWord;

public sealed class CreateCustomWordHandler(ILearnerLibraryRepository library, LibraryWordService words,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<CreateCustomWordHandler> logger)
    : IRequestHandler<CreateCustomWordCommand, SavedWordDto>
{
    public async Task<SavedWordDto> Handle(CreateCustomWordCommand request, CancellationToken ct)
    {
        await words.EnsureFolderAsync(request.LearnerId, request.FolderId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var word = new VocabularyWord();
        await words.ApplyCustomAsync(word, request.LearnerId, request.ToInput(), now, ct);
        var saved = new SavedWord
        {
            LearnerId = request.LearnerId, WordId = word.Id, Word = word, FolderId = request.FolderId,
            Note = LibraryWordService.Clean(request.Note), SavedAt = now, UpdatedAt = now
        };
        library.AddWord(word);
        library.AddSavedWord(saved);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Learner {LearnerId} created custom word {WordId}", request.LearnerId, word.Id);
        return await words.ToDtoAsync(saved, request.TzOffset, ct);
    }
}
