using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Mappings;

public static class GrammarMappingExtensions
{
    public static CommentDto ToDto(this GrammarComment comment, IReadOnlySet<long> liked, Guid? viewerId,
        IReadOnlyList<CommentDto>? replies = null) => new(
        comment.Id.ToString(),
        comment.ParentId?.ToString(),
        comment.Content,
        DateTime.SpecifyKind(comment.CreatedAt, DateTimeKind.Utc),
        comment.LikeCount,
        liked.Contains(comment.Id),
        comment.ReplyCount,
        new CommentAuthorDto(comment.LearnerId.ToString(), comment.AuthorName),
        viewerId is not null && comment.LearnerId == viewerId,
        replies);

    public static GrammarPreferencesDto ToPreferencesDto(this GrammarLearnerState? state) =>
        new(state?.DarkMode, state?.RoadmapWidth, state?.RoadmapCollapsed ?? false);
}
