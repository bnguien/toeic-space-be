namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// A comment on a grammar topic document (<see cref="Target"/> = <c>topic-04:document</c>). Replies are one level deep:
/// <see cref="ParentId"/> always points at a root. Deleting is soft; deleting a root hides its replies.
/// <see cref="AuthorName"/> is the display name captured at write time (no cross-service lookup).
/// </summary>
public sealed class GrammarComment
{
    public const int MaxLength = 1500;

    public long Id { get; set; }
    public string Target { get; set; } = string.Empty;
    public Guid LearnerId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int LikeCount { get; set; }
    public int ReplyCount { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A learner's like of a comment (one per learner and comment).</summary>
public sealed class GrammarCommentLike
{
    public long CommentId { get; set; }
    public Guid LearnerId { get; set; }
    public DateTime CreatedAt { get; set; }
}
