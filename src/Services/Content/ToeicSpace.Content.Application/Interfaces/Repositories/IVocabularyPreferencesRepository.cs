using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface IVocabularyPreferencesRepository
{
    Task<LearnerVocabularyPreferences?> GetAsync(Guid learnerId, CancellationToken ct);
    Task<LearnerVocabularyPreferences?> GetForUpdateAsync(Guid learnerId, CancellationToken ct);
    void Add(LearnerVocabularyPreferences preferences);
}
