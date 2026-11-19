using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.UpdateGrammarPreferences;

/// <summary>Grammar page preferences: dark mode (null = never chosen), roadmap width (300..720 or null), collapsed.</summary>
public sealed record UpdateGrammarPreferencesCommand(bool? DarkMode, int? RoadmapWidth, bool RoadmapCollapsed)
    : IRequest<GrammarPreferencesDto>
{
    public Guid LearnerId { get; init; }
}
