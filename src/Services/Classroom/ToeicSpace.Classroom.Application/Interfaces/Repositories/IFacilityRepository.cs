using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Interfaces.Repositories;

public interface IFacilityRepository
{
    Task<Center?> CenterAsync(Guid id, CancellationToken ct);
    Task<PagedResult<Center>> CentersAsync(string? search, FacilityStatus? status, int page, int size, CancellationToken ct);
    Task<Room?> RoomAsync(Guid id, CancellationToken ct);
    Task<PagedResult<Room>> RoomsAsync(Guid? centerId, string? search, FacilityStatus? status,
        int page, int size, CancellationToken ct);
    Task<bool> RoomNameExistsAsync(Guid centerId, string name, Guid? exceptId, CancellationToken ct);
    Task<int> ActiveEnrollmentCountAsync(Guid classId, CancellationToken ct);
    Task<int> MaxActiveEnrollmentCountForRoomAsync(Guid roomId, CancellationToken ct);
    Task<Class?> LockClassAsync(Guid id, CancellationToken ct);
    Task<Center?> LockCenterAsync(Guid id, CancellationToken ct);
    Task<Room?> LockRoomAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ClassRoomChange>> RoomHistoryAsync(Guid classId, CancellationToken ct);
    void Add(Center center);
    void Add(Room room);
    void Add(ClassRoomChange change);
}
