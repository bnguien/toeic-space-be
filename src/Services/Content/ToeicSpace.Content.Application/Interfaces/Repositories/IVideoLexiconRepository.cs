using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

/// <summary>Official roadmap words used by the word card, and the learner's library as the reader sees it.</summary>
public interface IVideoLexiconRepository
{
    /// <summary>Synonyms of the official word(s) with this exact normalized headword.</summary>
    Task<IReadOnlyList<string>> GetOfficialSynonymsAsync(string normalizedHeadword, CancellationToken ct);
    /// <summary>Official words starting with <paramref name="stem"/> (other than the word itself), by headword.</summary>
    Task<IReadOnlyList<OfficialWordEntry>> GetOfficialFamilyAsync(string stem, string excludeHeadword, int take,
        CancellationToken ct);
    /// <summary>The learner's saved words with their word, newest first.</summary>
    Task<IReadOnlyList<SavedWord>> GetSavedWordsAsync(Guid learnerId, int take, CancellationToken ct);
    Task<int> CountSavedWordsAsync(Guid learnerId, CancellationToken ct);
}
