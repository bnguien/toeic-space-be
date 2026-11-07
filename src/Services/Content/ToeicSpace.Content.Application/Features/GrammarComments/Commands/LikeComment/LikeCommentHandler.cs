using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Commands.LikeComment;

public sealed class LikeCommentHandler(IGrammarCommentRepository repository, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<LikeCommentCommand, CommentLikeDto>
{
    public async Task<CommentLikeDto> Handle(LikeCommentCommand request, CancellationToken ct)
    {
        var comment = await repository.GetLiveAsync(request.CommentId, ct) ?? throw GrammarErrors.CommentNotFound();
        var like = await repository.GetLikeAsync(comment.Id, request.LearnerId, ct);
        if (request.Liked && like is null)
        {
            repository.AddLike(new GrammarCommentLike
                { CommentId = comment.Id, LearnerId = request.LearnerId, CreatedAt = clock.GetUtcNow().UtcDateTime });
            await unitOfWork.SaveChangesAsync(ct);
            await repository.AdjustLikeCountAsync(comment.Id, 1, ct);
        }
        else if (!request.Liked && like is not null)
        {
            repository.RemoveLike(like);
            await unitOfWork.SaveChangesAsync(ct);
            await repository.AdjustLikeCountAsync(comment.Id, -1, ct);
        }
        return new CommentLikeDto(comment.Id.ToString(), request.Liked, await repository.GetLikeCountAsync(comment.Id, ct));
    }
}
