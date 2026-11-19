using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class VocabularyImportRepository(ContentDbContext db) : IVocabularyImportRepository
{
    public Task<VocabularyStage?> GetStageForUpdateAsync(string slug, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VocabularyStages
            .Include(x => x.Sets).ThenInclude(x => x.Words)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Slug == slug, ct));

    public Task<IReadOnlyList<VocabularyWord>> GetWordsForUpdateAsync(IReadOnlyCollection<string> normalizedHeadwords,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<VocabularyWord>>(async () =>
        {
            var words = new List<VocabularyWord>(normalizedHeadwords.Count);
            // Chunked to keep the IN list of a 1,300-word stage file reasonable.
            foreach (var chunk in normalizedHeadwords.Chunk(500))
                words.AddRange(await db.VocabularyWords
                    .Where(x => x.OwnerKey == Guid.Empty && chunk.Contains(x.NormalizedHeadword)).ToListAsync(ct));
            return words;
        });

    public void AddStage(VocabularyStage stage) => db.VocabularyStages.Add(stage);
    public void AddSet(VocabularySet set) => db.VocabularySets.Add(set);
    public void AddWord(VocabularyWord word) => db.VocabularyWords.Add(word);
    public void AddSetWord(VocabularySetWord link) => db.VocabularySetWords.Add(link);
    public void RemoveSetWord(VocabularySetWord link) => db.VocabularySetWords.Remove(link);
}
