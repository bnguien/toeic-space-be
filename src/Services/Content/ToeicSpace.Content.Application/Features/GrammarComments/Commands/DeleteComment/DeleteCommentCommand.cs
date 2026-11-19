using ToeicSpace.Content.Application.DTOs.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Commands.DeleteComment;

/// <summary>Soft-deletes one's own comment (admins: any); deleting a root hides its replies.</summary>
public sealed record DeleteCommentCommand(long CommentId, Guid LearnerId, bool IsAdmin) : IRequest<CommentDeleteDto>;
