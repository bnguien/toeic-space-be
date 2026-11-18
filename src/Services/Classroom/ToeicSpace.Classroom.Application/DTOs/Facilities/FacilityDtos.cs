using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.DTOs.Facilities;

public sealed record CenterDto(Guid Id, string Name, string Address, string TimeZoneId, FacilityStatus Status,
    DateTime CreatedAt, DateTime UpdatedAt)
{
    public static CenterDto From(Center x) => new(x.Id, x.Name, x.Address, x.TimeZoneId, x.Status, x.CreatedAt, x.UpdatedAt);
}

public sealed record RoomDto(Guid Id, Guid CenterId, string CenterName, string Name, int Capacity,
    FacilityStatus Status, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static RoomDto From(Room x) => new(x.Id, x.CenterId, x.Center.Name, x.Name, x.Capacity,
        x.Status, x.CreatedAt, x.UpdatedAt);
}

public sealed record ClassRoomChangeDto(Guid Id, Guid ClassId, Guid? PreviousRoomId, Guid? NewRoomId,
    Guid ChangedBy, DateTime ChangedAt, string? Reason)
{
    public static ClassRoomChangeDto From(ClassRoomChange x) => new(x.Id, x.ClassId, x.PreviousRoomId,
        x.NewRoomId, x.ChangedBy, x.ChangedAt, x.Reason);
}
