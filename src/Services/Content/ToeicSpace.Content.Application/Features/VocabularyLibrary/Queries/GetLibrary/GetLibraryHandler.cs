using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetLibrary;

public sealed class GetLibraryHandler(ILearnerLibraryRepository library, ILearnerProgressRepository progress,
    TimeProvider clock) : IRequestHandler<GetLibraryQuery, LibraryDto>
{
    public async Task<LibraryDto> Handle(GetLibraryQuery request, CancellationToken ct)
    {
        var today = SpacedRepetitionScheduler.LocalDay(clock.GetUtcNow().UtcDateTime, request.TzOffset);
        var folders = await library.GetFoldersAsync(request.LearnerId, ct);
        var words = await library.GetSavedWordsAsync(request.LearnerId, ct);
        var rows = (await progress.GetForWordsAsync(request.LearnerId, words.Select(x => x.WordId).ToArray(), ct))
            .ToDictionary(x => x.WordId);
        var byFolder = words.ToLookup(x => x.FolderId);
        return new LibraryDto(words.Count, LibraryCounter.Count(byFolder[null], rows, today),
            folders.Select(f => f.ToDto(LibraryCounter.Count(byFolder[f.Id], rows, today))).ToArray());
    }
}
