using ToeicSpace.Content.Application.DTOs.VocabularyPreferences;

namespace ToeicSpace.Content.Application.Features.VocabularyPreferences.Queries.GetPreferences;

/// <summary>The learner's preferences, or the defaults when nothing was saved yet.</summary>
public sealed record GetPreferencesQuery(Guid LearnerId) : IRequest<PreferencesDto>;
