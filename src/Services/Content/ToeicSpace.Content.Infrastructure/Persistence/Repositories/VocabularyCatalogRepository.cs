using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Vocabulary;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class VocabularyCatalogRepository(ContentDbContext db) : IVocabularyCatalogRepository
{
    private IQueryable<VocabularySet> ActiveSets => db.VocabularySets.AsNoTracking()
        .Where(x => x.IsActive && x.Stage!.IsActive);

    public Task<IReadOnlyList<VocabularyStage>> GetStagesAsync(CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<VocabularyStage>>(async () =>
            await db.VocabularyStages.AsNoTracking().Where(x => x.IsActive)
                .OrderBy(x => x.Order).ThenBy(x => x.Slug).ToListAsync(ct));

    public Task<bool> StageExistsAsync(Guid stageId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.VocabularyStages.AnyAsync(x => x.Id == stageId && x.IsActive, ct));

    public Task<IReadOnlyList<VocabularySet>> GetSetsWithWordsAsync(Guid stageId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<VocabularySet>>(async () =>
            await ActiveSets.Where(x => x.StageId == stageId)
                .Include(x => x.Words).ThenInclude(x => x.Word)
                .OrderBy(x => x.Order).ThenBy(x => x.Slug)
                .AsSplitQuery().ToListAsync(ct));

    public Task<VocabularySet?> GetSetDetailAsync(Guid setId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            ActiveSets.Where(x => x.Id == setId)
                .Include(x => x.Stage)
                .Include(x => x.Words).ThenInclude(x => x.Word)
                .AsSplitQuery().FirstOrDefaultAsync(ct));

    public Task<IReadOnlyList<Guid>> GetOrderedSetIdsAsync(CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<Guid>>(async () =>
            await ActiveSets.OrderBy(x => x.Stage!.Order).ThenBy(x => x.Stage!.Slug)
                .ThenBy(x => x.Order).ThenBy(x => x.Slug)
                .Select(x => x.Id).ToListAsync(ct));

    public Task<IReadOnlyList<SetWordMembership>> GetMembershipAsync(Guid? stageId,
        IReadOnlyCollection<Guid>? wordIds, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<SetWordMembership>>(async () =>
        {
            var query = db.VocabularySetWords.AsNoTracking()
                .Where(x => x.Set!.IsActive && x.Set.Stage!.IsActive);
            if (stageId is { } stage) query = query.Where(x => x.Set!.StageId == stage);
            if (wordIds is not null) query = query.Where(x => wordIds.Contains(x.WordId));
            return await query.Select(x => new SetWordMembership(x.Set!.StageId, x.Set.Stage!.Order, x.SetId,
                x.Set.Order, x.WordId)).ToListAsync(ct);
        });

    public Task<IReadOnlyList<SetOverview>> GetSetOverviewsAsync(CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<SetOverview>>(async () =>
            await ActiveSets.OrderBy(x => x.Stage!.Order).ThenBy(x => x.Stage!.Slug)
                .ThenBy(x => x.Order).ThenBy(x => x.Slug)
                .Select(x => new SetOverview(x.StageId, x.Stage!.Order, x.Id, x.Order, x.Title, x.Words.Count))
                .ToListAsync(ct));

    public Task<IReadOnlyList<VocabularyWord>> GetWordsAsync(IReadOnlyCollection<Guid> wordIds, Guid learnerId,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<VocabularyWord>>(async () =>
            await db.VocabularyWords.AsNoTracking()
                .Where(x => wordIds.Contains(x.Id) && (x.OwnerKey == Guid.Empty || x.OwnerKey == learnerId))
                .ToListAsync(ct));

    public Task<bool> IsWordAccessibleAsync(Guid wordId, Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VocabularyWords.AnyAsync(x => x.Id == wordId
            && (x.OwnerKey == Guid.Empty || x.OwnerKey == learnerId), ct));

    public Task<IReadOnlyList<Guid>> GetOfficialWordIdsAsync(PartOfSpeech partOfSpeech, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<Guid>>(async () =>
            await db.VocabularyWords.AsNoTracking()
                .Where(x => x.OwnerKey == Guid.Empty && x.PartOfSpeech == partOfSpeech)
                .Select(x => x.Id).ToListAsync(ct));

    public Task<IReadOnlyList<VocabularyWord>> GetStageWordsAsync(Guid stageId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<VocabularyWord>>(async () =>
        {
            var ids = db.VocabularySetWords
                .Where(x => x.Set!.StageId == stageId && x.Set.IsActive)
                .Select(x => x.WordId);
            return await db.VocabularyWords.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        });
}
