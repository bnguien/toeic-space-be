using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.GrammarComments.Queries.GetCommentReplies;

public sealed class GetCommentRepliesHandler(IGrammarCommentRepository repository)
    : IRequestHandler<GetCommentRepliesQuery, IReadOnlyList<CommentDto>>
{
    public async Task<IReadOnlyList<CommentDto>> Handle(GetCommentRepliesQuery request, CancellationToken ct)
    {
        var root = await repository.GetLiveAsync(request.RootId, ct);
        if (root is null || root.ParentId is not null) return [];
        var replies = await repository.ListRepliesAsync([root.Id], 200, ct);
        var liked = request.ViewerId is { } viewer
            ? await repository.LikedIdsAsync(viewer, replies.Select(r => r.Id).ToList(), ct)
            : new HashSet<long>();
        return replies.Select(r => r.ToDto(liked, request.ViewerId)).ToList();
    }
}
