using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Interfaces.Services.Video;

/// <summary>The CEFR/TOEIC word list and the basic word list (static reference data).</summary>
public interface IVideoWordLevelSource
{
    WordLevelTable Table { get; }
}
