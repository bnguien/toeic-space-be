using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClasses;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;

public sealed class ClassRepository(ClassroomDbContext db) : IClassRepository
{
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Classes.AnyAsync(x => x.Code == code, ct));

    public Task<Class?> GetByIdAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Classes.Include(x => x.Instructors)
            .FirstOrDefaultAsync(x => x.Id == id && (includeDeleted || x.Status != ClassStatus.Deleted), cancellationToken));

    public Task<Class?> GetDetailAsync(Guid id, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Classes.AsNoTracking().Include(x => x.Instructors)
            .FirstOrDefaultAsync(x => x.Id == id && x.Status != ClassStatus.Deleted, ct));

    public async Task<PagedResult<Class>> SearchAsync(ClassSearchCriteria search, CancellationToken ct)
    {
        var query = FilterSearch(db.Classes.AsNoTracking().Where(x => x.Status != ClassStatus.Deleted), search.Search);

        if (search.Status is { } status) query = query.Where(x => x.Status == status);
        if (search.TeacherId is { } teacherId)
            query = query.Where(x => x.Instructors.Any(i => i.TeacherId == teacherId && i.UnassignedAt == null));
        if (search.StartDateFrom is { } from) query = query.Where(x => x.StartDate >= from);
        if (search.StartDateTo is { } to) query = query.Where(x => x.StartDate <= to);

        return await PersistenceExceptionTranslator.ExecuteAsync(() => PageAsync(query, search.Page, search.PageSize, ct));
    }

    public Task<PagedResult<Class>> GetDeletedAsync(string? search, int page, int pageSize, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => PageAsync(
            FilterSearch(db.Classes.AsNoTracking().Where(x => x.Status == ClassStatus.Deleted), search), page, pageSize, ct));

    private static IQueryable<Class> FilterSearch(IQueryable<Class> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(x => x.Code.Contains(term) || x.Name.Contains(term));
    }

    public Task<PagedResult<Class>> GetTeacherClassesAsync(Guid teacherId, int page, int pageSize, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => PageAsync(db.Classes.AsNoTracking().Where(x => x.Status != ClassStatus.Deleted &&
            x.Instructors.Any(i => i.TeacherId == teacherId && i.UnassignedAt == null)), page, pageSize, ct));

    public Task<PagedResult<StudentClassView>> GetStudentClassesAsync(Guid studentId, string? search, ClassStatus? status,
        int page, int pageSize, CancellationToken ct)
    {
        var query = FilterSearch(db.Classes.AsNoTracking().Where(x => x.Status != ClassStatus.Deleted
            && x.Enrollments.Any(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active
                && e.WithdrawnAt == null)), search);
        if (status is { } filter) query = query.Where(x => x.Status == filter);
        return PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            var result = await PageAsync(query, page, pageSize, ct);
            var teacherIds = result.Items.Select(x => x.Instructors.SingleOrDefault(i => i.UnassignedAt == null)?.TeacherId)
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
            var teachers = await db.Teachers.AsNoTracking().Where(x => teacherIds.Contains(x.TeacherId))
                .ToDictionaryAsync(x => x.TeacherId, ct);
            var items = result.Items.Select(x =>
            {
                var teacherId = x.Instructors.SingleOrDefault(i => i.UnassignedAt == null)?.TeacherId;
                return new StudentClassView(x, teacherId is { } id && teachers.TryGetValue(id, out var teacher)
                    ? teacher.Name : null);
            }).ToArray();
            return new PagedResult<StudentClassView>(items, result.Page, result.PageSize, result.TotalCount);
        });
    }

    public Task<StudentClassView?> GetStudentDetailAsync(Guid studentId, Guid classId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            var entity = await db.Classes.AsNoTracking().Include(x => x.Instructors)
                .FirstOrDefaultAsync(x => x.Id == classId && x.Status != ClassStatus.Deleted
                    && x.Enrollments.Any(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active
                        && e.WithdrawnAt == null), ct);
            if (entity is null) return null;
            var teacherId = entity.Instructors.SingleOrDefault(i => i.UnassignedAt == null)?.TeacherId;
            var teacherName = teacherId is { } id
                ? await db.Teachers.AsNoTracking().Where(x => x.TeacherId == id).Select(x => x.Name)
                    .FirstOrDefaultAsync(ct) : null;
            return new StudentClassView(entity, teacherName);
        });

    public async Task<IReadOnlyList<Guid>> GetDraftIdsReadyToStartAsync(DateOnly today, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.Classes.AsNoTracking()
            .Where(x => x.Status == ClassStatus.Draft && x.StartDate != null && x.StartDate <= today
                && x.EndDate != null && x.EndDate >= today)
            .OrderBy(x => x.StartDate).ThenBy(x => x.Id).Select(x => x.Id).ToListAsync(ct));

    private static async Task<PagedResult<Class>> PageAsync(IQueryable<Class> query, int page, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Include(x => x.Instructors).OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<Class>(items, page, pageSize, total);
    }

    public void Add(Class entity) => db.Classes.Add(entity);
    public void Add(ClassInstructor entity) => db.ClassInstructors.Add(entity);
    public void Remove(Class entity) => db.Classes.Remove(entity);
}
