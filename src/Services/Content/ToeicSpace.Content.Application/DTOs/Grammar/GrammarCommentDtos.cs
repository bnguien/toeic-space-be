namespace ToeicSpace.Content.Application.DTOs.Grammar;

public sealed record CommentAuthorDto(string Id, string Name);

public sealed record CommentDto(
    string Id,
    string? ParentId,
    string Content,
    DateTime CreatedAt,
    int Likes,
    bool Liked,
    int ReplyCount,
    CommentAuthorDto Author,
    bool Mine,
    IReadOnlyList<CommentDto>? Replies);

public sealed record CommentPageDto(IReadOnlyList<CommentDto> Items, string? Next, int Total);

public sealed record CommentLikeDto(string Id, bool Liked, int Likes);

public sealed record CommentDeleteDto(string Id, bool Deleted);
