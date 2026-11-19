using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class LearnerLibraryRepository(ContentDbContext db) : ILearnerLibraryRepository
{
    public Task<IReadOnlyList<VocabularyFolder>> GetFoldersAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<VocabularyFolder>>(async () =>
            await db.VocabularyFolders.AsNoTracking().Where(x => x.LearnerId == learnerId)
                .OrderBy(x => x.Order).ThenBy(x => x.Name).ToListAsync(ct));

    public Task<VocabularyFolder?> GetFolderForUpdateAsync(Guid learnerId, Guid folderId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.VocabularyFolders.FirstOrDefaultAsync(x => x.Id == folderId && x.LearnerId == learnerId, ct));

    public Task<bool> FolderExistsAsync(Guid learnerId, Guid folderId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.VocabularyFolders.AnyAsync(x => x.Id == folderId && x.LearnerId == learnerId, ct));

    public Task<bool> FolderNameExistsAsync(Guid learnerId, string normalizedName, Guid? excludeId,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VocabularyFolders.AnyAsync(x =>
            x.LearnerId == learnerId && x.NormalizedName == normalizedName
            && (!excludeId.HasValue || x.Id != excludeId.Value), ct));

    public Task<int> GetMaxFolderOrderAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
            await db.VocabularyFolders.Where(x => x.LearnerId == learnerId)
                .MaxAsync(x => (int?)x.Order, ct) ?? 0);

    public void AddFolder(VocabularyFolder folder) => db.VocabularyFolders.Add(folder);
    public void RemoveFolder(VocabularyFolder folder) => db.VocabularyFolders.Remove(folder);

    public Task<IReadOnlyList<SavedWord>> GetSavedWordsAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<SavedWord>>(async () =>
            await db.SavedWords.AsNoTracking().Include(x => x.Word)
                .Where(x => x.LearnerId == learnerId).ToListAsync(ct));

    public Task<IReadOnlyList<SavedWord>> GetSavedWordsForUpdateAsync(Guid learnerId, Guid? folderId,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<SavedWord>>(async () =>
            await db.SavedWords.Include(x => x.Word)
                .Where(x => x.LearnerId == learnerId && x.FolderId == folderId).ToListAsync(ct));

    public Task<PagedResult<SavedWord>> SearchSavedWordsAsync(Guid learnerId, Guid? folderId, bool unfiledOnly,
        string? search, int page, int pageSize, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            var query = db.SavedWords.AsNoTracking().Include(x => x.Word).Where(x => x.LearnerId == learnerId);
            if (folderId is { } folder) query = query.Where(x => x.FolderId == folder);
            else if (unfiledOnly) query = query.Where(x => x.FolderId == null);
            if (search is not null)
            {
                var term = search.ToLower();
                query = query.Where(x => x.Word!.NormalizedHeadword.Contains(term)
                    || x.Word.MeaningVi.ToLower().Contains(term)
                    || (x.MyMeaningVi != null && x.MyMeaningVi.ToLower().Contains(term)));
            }
            var count = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.SavedAt).ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return new PagedResult<SavedWord>(items, page, pageSize, count);
        });

    public Task<SavedWord?> GetSavedWordForUpdateAsync(Guid learnerId, Guid savedWordId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.SavedWords.Include(x => x.Word)
            .FirstOrDefaultAsync(x => x.Id == savedWordId && x.LearnerId == learnerId, ct));

    public Task<SavedWord?> GetSavedWordByWordForUpdateAsync(Guid learnerId, Guid wordId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.SavedWords.Include(x => x.Word)
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId && x.WordId == wordId, ct));

    public Task<IReadOnlyList<SavedWord>> GetSavedWordsByWordAsync(Guid learnerId,
        IReadOnlyCollection<Guid> wordIds, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<SavedWord>>(async () =>
            await db.SavedWords.AsNoTracking()
                .Where(x => x.LearnerId == learnerId && wordIds.Contains(x.WordId)).ToListAsync(ct));

    public Task<IReadOnlyList<SavedWord>> GetPersonalizedAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<SavedWord>>(async () =>
            await db.SavedWords.AsNoTracking()
                .Where(x => x.LearnerId == learnerId && (x.MyPhonetic != null || x.MyPartOfSpeech != null
                    || x.MyMeaningVi != null || x.MyExample != null || x.MyExampleVi != null))
                .ToListAsync(ct));

    public Task<int> CountSavedWordsAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.SavedWords.CountAsync(x => x.LearnerId == learnerId, ct));

    public void AddSavedWord(SavedWord savedWord) => db.SavedWords.Add(savedWord);
    public void RemoveSavedWord(SavedWord savedWord) => db.SavedWords.Remove(savedWord);

    public Task<bool> CustomWordExistsAsync(Guid learnerId, string normalizedHeadword, PartOfSpeech partOfSpeech,
        Guid? excludeWordId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VocabularyWords.AnyAsync(x =>
            x.OwnerKey == learnerId && x.NormalizedHeadword == normalizedHeadword && x.PartOfSpeech == partOfSpeech
            && (!excludeWordId.HasValue || x.Id != excludeWordId.Value), ct));

    public Task<IReadOnlyList<VocabularyWord>> GetCustomWordsAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<VocabularyWord>>(async () =>
            await db.VocabularyWords.AsNoTracking().Where(x => x.OwnerKey == learnerId).ToListAsync(ct));

    public Task<IReadOnlyList<VocabularyWord>> GetCustomWordsForUpdateAsync(Guid learnerId,
        IReadOnlyCollection<string> normalizedHeadwords, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<VocabularyWord>>(async () =>
            await db.VocabularyWords.Where(x => x.OwnerKey == learnerId
                && normalizedHeadwords.Contains(x.NormalizedHeadword)).ToListAsync(ct));

    public Task<IReadOnlyList<SavedWord>> FindSavedByHeadwordAsync(Guid learnerId, string normalizedHeadword,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<SavedWord>>(async () =>
            await db.SavedWords.AsNoTracking().Include(x => x.Word)
                .Where(x => x.LearnerId == learnerId && x.Word!.NormalizedHeadword == normalizedHeadword)
                .ToListAsync(ct));

    public void AddWord(VocabularyWord word) => db.VocabularyWords.Add(word);
    public void RemoveWord(VocabularyWord word) => db.VocabularyWords.Remove(word);
}
