using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Queries.GetComments;

public sealed class GetCommentsHandler(IGrammarCommentRepository repository) : IRequestHandler<GetCommentsQuery, CommentPageDto>
{
    public async Task<CommentPageDto> Handle(GetCommentsQuery request, CancellationToken ct)
    {
        var total = await repository.CountAsync(request.Target, ct);
        var roots = (await repository.ListRootsAsync(request.Target, request.Before, GrammarCommentRules.PageSize + 1, ct)).ToList();
        var more = roots.Count > GrammarCommentRules.PageSize;
        roots = roots.Take(GrammarCommentRules.PageSize).ToList();
        var withReplies = roots.Where(r => r.ReplyCount > 0).Select(r => r.Id).ToList();
        var replies = withReplies.Count == 0 ? []
            : (await repository.ListRepliesAsync(withReplies, GrammarCommentRules.PageSize * 20, ct))
                .GroupBy(r => r.ParentId!.Value)
                .SelectMany(g => g.Take(GrammarCommentRules.RepliesPreview)).ToList();
        var liked = request.ViewerId is { } viewer
            ? await repository.LikedIdsAsync(viewer, [.. roots.Select(r => r.Id), .. replies.Select(r => r.Id)], ct)
            : new HashSet<long>();
        var items = roots.Select(root => root.ToDto(liked, request.ViewerId,
            replies.Where(r => r.ParentId == root.Id).Select(r => r.ToDto(liked, request.ViewerId)).ToList())).ToList();
        return new CommentPageDto(items, more && roots.Count > 0 ? roots[^1].Id.ToString() : null, total);
    }
}
