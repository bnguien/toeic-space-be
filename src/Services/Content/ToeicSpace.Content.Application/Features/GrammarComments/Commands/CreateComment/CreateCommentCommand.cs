using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Commands.CreateComment;

/// <summary>
/// Posts a comment or a reply (a reply to a reply is attached to its root). <c>AuthorName</c> is resolved by the API
/// (token <c>name</c> claim when Identity issues one, otherwise the display name sent by the client).
/// </summary>
public sealed record CreateCommentCommand(string Target, string Content, long? ParentId) : IRequest<CommentDto>
{
    public Guid LearnerId { get; init; }
    public string AuthorName { get; init; } = string.Empty;
}
