using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class VideoLexiconRepository(ContentDbContext db) : IVideoLexiconRepository
{
    public async Task<IReadOnlyList<string>> GetOfficialSynonymsAsync(string normalizedHeadword, CancellationToken ct)
    {
        var lists = await PersistenceExceptionTranslator.ExecuteAsync(() => db.VocabularyWords.AsNoTracking()
            .Where(x => x.OwnerKey == Guid.Empty && x.NormalizedHeadword == normalizedHeadword)
            .OrderBy(x => x.CreatedAt).Select(x => x.Synonyms).ToListAsync(ct));
        return lists.SelectMany(x => x).ToList();
    }

    public async Task<IReadOnlyList<OfficialWordEntry>> GetOfficialFamilyAsync(string stem, string excludeHeadword, int take,
        CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.VocabularyWords.AsNoTracking()
            .Where(x => x.OwnerKey == Guid.Empty && x.NormalizedHeadword.StartsWith(stem)
                        && x.NormalizedHeadword != excludeHeadword)
            .OrderBy(x => x.NormalizedHeadword).Take(take)
            .Select(x => new OfficialWordEntry(x.Headword, x.MeaningVi, x.PartOfSpeech)).ToListAsync(ct));

    public async Task<IReadOnlyList<SavedWord>> GetSavedWordsAsync(Guid learnerId, int take, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.SavedWords.AsNoTracking().Include(x => x.Word)
            .Where(x => x.LearnerId == learnerId).OrderByDescending(x => x.SavedAt).Take(take).ToListAsync(ct));

    public Task<int> CountSavedWordsAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.SavedWords.CountAsync(x => x.LearnerId == learnerId, ct));
}
