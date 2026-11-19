using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Commands.DeleteComment;

public sealed class DeleteCommentHandler(IGrammarCommentRepository repository)
    : IRequestHandler<DeleteCommentCommand, CommentDeleteDto>
{
    public async Task<CommentDeleteDto> Handle(DeleteCommentCommand request, CancellationToken ct)
    {
        var comment = await repository.GetLiveAsync(request.CommentId, ct) ?? throw GrammarErrors.CommentNotFound();
        if (comment.LearnerId != request.LearnerId && !request.IsAdmin) throw GrammarErrors.CommentForbidden();
        await repository.SoftDeleteAsync(comment, ct);
        if (comment.ParentId is { } rootId) await repository.AdjustReplyCountAsync(rootId, -1, ct);
        return new CommentDeleteDto(comment.Id.ToString(), true);
    }
}
