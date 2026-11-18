using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Domain.Entities;

public static class SchedulingRules
{
    public static bool Conflicts(ClassSession a, ClassSession b) =>
        // Half-open UTC intervals: adjacent lessons never conflict.
        a.StartAt < b.EndAt && a.EndAt > b.StartAt &&
        (a.TeacherId == b.TeacherId || a.Mode == SessionMode.Offline && b.Mode == SessionMode.Offline &&
            a.RoomId.HasValue && a.RoomId == b.RoomId);

    public static DateTime LocalToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new ClassRuleException("ambiguous_schedule_time", "Schedule crosses a DST transition.", 409);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}