using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class VocabularyPreferencesRepository(ContentDbContext db) : IVocabularyPreferencesRepository
{
    public Task<LearnerVocabularyPreferences?> GetAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.LearnerVocabularyPreferences.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId, ct));

    public Task<LearnerVocabularyPreferences?> GetForUpdateAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.LearnerVocabularyPreferences
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId, ct));

    public void Add(LearnerVocabularyPreferences preferences) => db.LearnerVocabularyPreferences.Add(preferences);
}
