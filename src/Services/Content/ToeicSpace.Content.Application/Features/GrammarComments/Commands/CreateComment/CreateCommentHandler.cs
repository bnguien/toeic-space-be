using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Commands.CreateComment;

public sealed class CreateCommentHandler(IGrammarCommentRepository repository, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<CreateCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(CreateCommentCommand request, CancellationToken ct)
    {
        var content = GrammarCommentRules.Normalize(request.Content);
        if (content.Length == 0) throw GrammarErrors.Invalid("Bình luận đang trống.");
        if (content.Length > GrammarComment.MaxLength)
            throw GrammarErrors.Invalid($"Bình luận tối đa {GrammarComment.MaxLength} ký tự.");

        GrammarComment? parent = null;
        if (request.ParentId is { } parentId)
        {
            parent = await repository.GetLiveAsync(parentId, ct) ?? throw GrammarErrors.CommentNotFound();
            if (parent.Target != request.Target) throw GrammarErrors.Invalid("The parent comment is not on this target.");
            if (parent.ParentId is { } rootId)
                parent = await repository.GetLiveAsync(rootId, ct) ?? throw GrammarErrors.CommentNotFound();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var last = await repository.GetLatestByLearnerAsync(request.LearnerId, ct);
        if (last is not null)
        {
            var age = now - last.CreatedAt;
            if (age < GrammarCommentRules.Cooldown) throw GrammarErrors.Invalid("Bạn bình luận nhanh quá, chờ vài giây nhé.");
            if (age < GrammarCommentRules.DuplicateWindow && last.Target == request.Target && last.Content == content)
                throw GrammarErrors.Invalid("Bạn vừa gửi bình luận y hệt.");
        }

        var comment = new GrammarComment
        {
            Target = request.Target,
            LearnerId = request.LearnerId,
            AuthorName = request.AuthorName,
            ParentId = parent?.Id,
            Content = content,
            CreatedAt = now
        };
        repository.Add(comment);
        await unitOfWork.SaveChangesAsync(ct);
        if (parent is not null) await repository.AdjustReplyCountAsync(parent.Id, 1, ct);
        return comment.ToDto(new HashSet<long>(), request.LearnerId, []);
    }
}
