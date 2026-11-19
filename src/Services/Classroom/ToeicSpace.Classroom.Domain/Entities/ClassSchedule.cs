using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class ClassSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public List<DayOfWeek> DaysOfWeek { get; set; } = [];
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid TeacherId { get; set; }
    public SessionMode Mode { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    // Both effective dates are inclusive. ExcludeDates remove occurrences in this range.
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveTo { get; set; }
    public List<DateOnly> ExcludeDates { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}
