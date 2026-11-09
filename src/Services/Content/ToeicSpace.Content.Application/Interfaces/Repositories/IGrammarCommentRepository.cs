using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface IGrammarCommentRepository
{
    /// <summary>Live root comments of a target, newest first, with id &lt; <paramref name="before"/>.</summary>
    Task<IReadOnlyList<GrammarComment>> ListRootsAsync(string target, long? before, int take, CancellationToken ct);
    /// <summary>Live roots + replies of a target.</summary>
    Task<int> CountAsync(string target, CancellationToken ct);
    /// <summary>Live replies of the given roots, oldest first.</summary>
    Task<IReadOnlyList<GrammarComment>> ListRepliesAsync(IReadOnlyCollection<long> rootIds, int take, CancellationToken ct);
    /// <summary>A live comment whose root (if any) is live too.</summary>
    Task<GrammarComment?> GetLiveAsync(long id, CancellationToken ct);
    Task<GrammarComment?> GetLatestByLearnerAsync(Guid learnerId, CancellationToken ct);
    Task<IReadOnlySet<long>> LikedIdsAsync(Guid learnerId, IReadOnlyCollection<long> commentIds, CancellationToken ct);
    Task<GrammarCommentLike?> GetLikeAsync(long commentId, Guid learnerId, CancellationToken ct);
    void Add(GrammarComment comment);
    void AddLike(GrammarCommentLike like);
    void RemoveLike(GrammarCommentLike like);
    Task AdjustLikeCountAsync(long commentId, int delta, CancellationToken ct);
    Task AdjustReplyCountAsync(long commentId, int delta, CancellationToken ct);
    /// <summary>Soft-deletes the comment and, for a root, its replies; returns the removed ids.</summary>
    Task<int> SoftDeleteAsync(GrammarComment comment, CancellationToken ct);
    Task<int> GetLikeCountAsync(long commentId, CancellationToken ct);
}
