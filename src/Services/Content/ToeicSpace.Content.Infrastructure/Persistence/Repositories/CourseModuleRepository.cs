using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class CourseModuleRepository(ContentDbContext db) : ICourseModuleRepository
{
    public Task<IReadOnlyList<CourseModule>> ListAsync(Guid courseId, bool tracking, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            var query = db.CourseModules.Where(x => x.CourseId == courseId);
            return (IReadOnlyList<CourseModule>)await (tracking ? query : query.AsNoTracking()).OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id).ToListAsync(ct);
        });

    public Task<CourseModule?> GetAsync(Guid courseId, Guid moduleId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.CourseModules
            .FirstOrDefaultAsync(x => x.Id == moduleId && x.CourseId == courseId, ct));

    public Task<bool> HasLessonsAsync(Guid moduleId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Lessons.AnyAsync(x => x.ModuleId == moduleId, ct));

    public Task ShiftOrdersAsync(Guid courseId, int offset, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            await db.CourseModules.Where(x => x.CourseId == courseId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + offset), ct);
            foreach (var entry in db.ChangeTracker.Entries<CourseModule>()
                         .Where(x => x.Entity.CourseId == courseId).ToArray())
                entry.State = EntityState.Detached;
            return true;
        });

    public void Add(CourseModule module) => db.CourseModules.Add(module);
    public void Remove(CourseModule module) => db.CourseModules.Remove(module);
}
