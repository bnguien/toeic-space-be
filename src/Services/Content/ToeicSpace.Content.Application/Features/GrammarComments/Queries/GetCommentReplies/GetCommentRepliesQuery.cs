using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Queries.GetCommentReplies;

/// <summary>Every live reply of a live root comment (oldest first, at most 200).</summary>
public sealed record GetCommentRepliesQuery(long RootId, Guid? ViewerId) : IRequest<IReadOnlyList<CommentDto>>;
