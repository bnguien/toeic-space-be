using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;

public sealed class FacilityRepository(ClassroomDbContext db) : IFacilityRepository
{
    public Task<Center?> CenterAsync(Guid id, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Centers.SingleOrDefaultAsync(x => x.Id == id, ct));

    public Task<PagedResult<Center>> CentersAsync(string? search, FacilityStatus? status, int page, int size,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            var query = db.Centers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x => x.Name.Contains(search.Trim()) || x.Address.Contains(search.Trim()));
            if (status is { } value) query = query.Where(x => x.Status == value);
            var total = await query.CountAsync(ct);
            var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
                .Skip((page - 1) * size).Take(size).ToListAsync(ct);
            return new PagedResult<Center>(items, page, size, total);
        });

    public Task<Room?> RoomAsync(Guid id, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.Rooms.Include(x => x.Center).SingleOrDefaultAsync(x => x.Id == id, ct));

    public Task<PagedResult<Room>> RoomsAsync(Guid? centerId, string? search, FacilityStatus? status,
        int page, int size, CancellationToken ct) => PersistenceExceptionTranslator.ExecuteAsync(async () =>
    {
        var query = db.Rooms.AsNoTracking().Include(x => x.Center).AsQueryable();
        if (centerId is { } id) query = query.Where(x => x.CenterId == id);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search.Trim()));
        if (status is { } value) query = query.Where(x => x.Status == value);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Center.Name).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<Room>(items, page, size, total);
    });

    public Task<bool> RoomNameExistsAsync(Guid centerId, string name, Guid? exceptId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Rooms.AnyAsync(x =>
            x.CenterId == centerId && x.Name == name && x.Id != exceptId, ct));

    public Task<int> ActiveEnrollmentCountAsync(Guid classId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.ClassEnrollments.CountAsync(x => x.ClassId == classId &&
            x.Status == EnrollmentStatus.Active && x.WithdrawnAt == null, ct));

    public Task<int> MaxActiveEnrollmentCountForRoomAsync(Guid roomId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            var classIds = await db.Classes.AsNoTracking().Where(x => x.DefaultRoomId == roomId)
                .Select(x => x.Id).ToListAsync(ct);
            var counts = await db.ClassEnrollments.AsNoTracking().Where(x => classIds.Contains(x.ClassId) &&
                                                                             x.Status == EnrollmentStatus.Active &&
                                                                             x.WithdrawnAt == null)
                .GroupBy(x => x.ClassId).Select(x => x.Count()).ToListAsync(ct);
            return counts.Count == 0 ? 0 : counts.Max();
        });

    public Task<Class?> LockClassAsync(Guid id, CancellationToken ct) => db.Database.IsRelational()
        ? PersistenceExceptionTranslator.ExecuteAsync(() => db.Classes
            .FromSqlInterpolated($"SELECT * FROM Classes WHERE Id = {id} FOR UPDATE").SingleOrDefaultAsync(ct))
        : db.Classes.SingleOrDefaultAsync(x => x.Id == id, ct);

    // Read a fresh row after the lock; RoomAsync may already have tracked an older Center/Room.
    public Task<Center?> LockCenterAsync(Guid id, CancellationToken ct) => db.Database.IsRelational()
        ? PersistenceExceptionTranslator.ExecuteAsync(() => db.Centers
            .FromSqlInterpolated($"SELECT * FROM Centers WHERE Id = {id} FOR UPDATE").AsNoTracking()
            .SingleOrDefaultAsync(ct))
        : db.Centers.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<Room?> LockRoomAsync(Guid id, CancellationToken ct) => db.Database.IsRelational()
        ? PersistenceExceptionTranslator.ExecuteAsync(() => db.Rooms
            .FromSqlInterpolated($"SELECT * FROM Rooms WHERE Id = {id} FOR UPDATE").AsNoTracking()
            .SingleOrDefaultAsync(ct))
        : db.Rooms.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<IReadOnlyList<ClassRoomChange>> RoomHistoryAsync(Guid classId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () => (IReadOnlyList<ClassRoomChange>)await db
            .ClassRoomChanges
            .AsNoTracking().Where(x => x.ClassId == classId).OrderBy(x => x.ChangedAt).ThenBy(x => x.Id)
            .ToListAsync(ct));

    public void Add(Center center) => db.Centers.Add(center);
    public void Add(Room room) => db.Rooms.Add(room);
    public void Add(ClassRoomChange change) => db.ClassRoomChanges.Add(change);
}