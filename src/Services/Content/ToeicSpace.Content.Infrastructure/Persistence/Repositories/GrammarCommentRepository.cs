using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class GrammarCommentRepository(ContentDbContext db) : IGrammarCommentRepository
{
    private IQueryable<GrammarComment> Live =>
        db.GrammarComments.Where(c => !c.IsDeleted
            && (c.ParentId == null || db.GrammarComments.Any(p => p.Id == c.ParentId && !p.IsDeleted)));

    public async Task<IReadOnlyList<GrammarComment>> ListRootsAsync(string target, long? before, int take, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarComments.AsNoTracking()
            .Where(c => c.Target == target && c.ParentId == null && !c.IsDeleted && (before == null || c.Id < before))
            .OrderByDescending(c => c.Id).Take(take).ToListAsync(ct));

    public Task<int> CountAsync(string target, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => Live.CountAsync(c => c.Target == target, ct));

    public async Task<IReadOnlyList<GrammarComment>> ListRepliesAsync(IReadOnlyCollection<long> rootIds, int take,
        CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarComments.AsNoTracking()
            .Where(c => c.ParentId != null && rootIds.Contains(c.ParentId.Value) && !c.IsDeleted)
            .OrderBy(c => c.ParentId).ThenBy(c => c.Id).Take(take).ToListAsync(ct));

    public Task<GrammarComment?> GetLiveAsync(long id, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => Live.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct));

    public Task<GrammarComment?> GetLatestByLearnerAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarComments.AsNoTracking()
            .Where(c => c.LearnerId == learnerId).OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct));

    public async Task<IReadOnlySet<long>> LikedIdsAsync(Guid learnerId, IReadOnlyCollection<long> commentIds, CancellationToken ct)
    {
        if (commentIds.Count == 0) return new HashSet<long>();
        var ids = await PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarCommentLikes.AsNoTracking()
            .Where(l => l.LearnerId == learnerId && commentIds.Contains(l.CommentId)).Select(l => l.CommentId).ToListAsync(ct));
        return ids.ToHashSet();
    }

    public Task<GrammarCommentLike?> GetLikeAsync(long commentId, Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.GrammarCommentLikes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.LearnerId == learnerId, ct));

    public void Add(GrammarComment comment) => db.GrammarComments.Add(comment);
    public void AddLike(GrammarCommentLike like) => db.GrammarCommentLikes.Add(like);
    public void RemoveLike(GrammarCommentLike like) => db.GrammarCommentLikes.Remove(like);

    public Task AdjustLikeCountAsync(long commentId, int delta, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarComments
            .Where(c => c.Id == commentId && (delta > 0 || c.LikeCount > 0))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LikeCount, c => c.LikeCount + delta), ct));

    public Task AdjustReplyCountAsync(long commentId, int delta, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarComments
            .Where(c => c.Id == commentId && (delta > 0 || c.ReplyCount > 0))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReplyCount, c => c.ReplyCount + delta), ct));

    public Task<int> SoftDeleteAsync(GrammarComment comment, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarComments
            .Where(c => c.Id == comment.Id || (comment.ParentId == null && c.ParentId == comment.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true), ct));

    public Task<int> GetLikeCountAsync(long commentId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarComments.Where(c => c.Id == commentId)
            .Select(c => c.LikeCount).FirstOrDefaultAsync(ct));
}
