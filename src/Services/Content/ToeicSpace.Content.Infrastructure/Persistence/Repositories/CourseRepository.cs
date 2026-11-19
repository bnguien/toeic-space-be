using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class CourseRepository(ContentDbContext db) : ICourseRepository
{
    public Task<Course?> GetAsync(Guid id, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Courses.FirstOrDefaultAsync(x => x.Id == id, ct));

    public Task<Course?> GetLockedAsync(Guid id, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Courses
            .FromSqlInterpolated($"SELECT * FROM Courses WHERE Id = {id} FOR UPDATE").SingleOrDefaultAsync(ct));

    public async Task<int> OccupiedSlotsAsync(Guid id, CancellationToken ct) =>
        await db.CourseEnrollments.CountAsync(x => x.CourseId == id, ct) +
        await db.CourseSlotReservations.CountAsync(x => x.CourseId == id &&
            x.Status == ToeicSpace.Content.Domain.Entities.SlotReservationStatus.Reserved, ct);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Courses.AnyAsync(x => x.Code == code &&
            (!excludeId.HasValue || x.Id != excludeId.Value), ct));

    public Task<bool> SlugExistsAsync(string slug, Guid? excludeId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Courses.AnyAsync(x => x.Slug == slug &&
            (!excludeId.HasValue || x.Id != excludeId.Value), ct));

    public Task<PagedResult<Course>> SearchAsync(string? search, CourseStatus? status, CourseLevel? level,
        CourseExamType? examType, int page, int pageSize, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            var query = db.Courses.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(x => x.Code.Contains(term) || x.Title.Contains(term));
            }
            if (status is { } s) query = query.Where(x => x.Status == s);
            if (level is { } l) query = query.Where(x => x.Level == l);
            if (examType is { } e) query = query.Where(x => x.ExamType == e);
            var count = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return new PagedResult<Course>(items, page, pageSize, count);
        });

    public void Add(Course course) => db.Courses.Add(course);
}
