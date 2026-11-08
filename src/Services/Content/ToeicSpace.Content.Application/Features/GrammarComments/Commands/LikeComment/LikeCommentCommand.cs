using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Commands.LikeComment;

/// <summary>Likes / unlikes a comment (idempotent).</summary>
public sealed record LikeCommentCommand(bool Liked) : IRequest<CommentLikeDto>
{
    public long CommentId { get; init; }
    public Guid LearnerId { get; init; }
}
