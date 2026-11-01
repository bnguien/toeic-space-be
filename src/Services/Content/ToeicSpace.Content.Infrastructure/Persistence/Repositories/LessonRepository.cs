using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class LessonRepository(ContentDbContext db) : ILessonRepository
{
    public Task<IReadOnlyList<Lesson>> ListAsync(Guid moduleId, bool tracking, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
            (IReadOnlyList<Lesson>)await (tracking ? db.Lessons : db.Lessons.AsNoTracking())
                .Include(x => x.Resources).Include(x => x.Videos).Where(x => x.ModuleId == moduleId)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct));
    public Task<Lesson?> GetAsync(Guid moduleId, Guid lessonId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Lessons.Include(x => x.Resources).Include(x => x.Videos)
            .FirstOrDefaultAsync(x => x.ModuleId == moduleId && x.Id == lessonId, ct));
    public Task<bool> IsObjectKeyUsedAsync(string key, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
            await db.LessonVideos.AnyAsync(x => x.ObjectKey == key, ct) ||
            await db.LessonResources.AnyAsync(x => x.ObjectKey == key, ct));
    public void AddVideo(LessonVideo video) => db.LessonVideos.Add(video);
    public void RemoveVideo(LessonVideo video) => db.LessonVideos.Remove(video);
    public Task ShiftOrdersAsync(Guid moduleId, int offset, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            await db.Lessons.Where(x => x.ModuleId == moduleId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + offset), ct);
            foreach (var entry in db.ChangeTracker.Entries<Lesson>()
                         .Where(x => x.Entity.ModuleId == moduleId).ToArray())
                entry.State = EntityState.Detached;
            return true;
        });
    public void Add(Lesson lesson) => db.Lessons.Add(lesson);
    public void Remove(Lesson lesson) => db.Lessons.Remove(lesson);
    public void AddResource(LessonResource resource) => db.LessonResources.Add(resource);
    public void RemoveResource(LessonResource resource) => db.LessonResources.Remove(resource);
}
