namespace ToeicSpace.Classroom.Domain.Entities;

// One persistent InnoDB row per teacher/room. Writers lock rows in ordinal key order.
public sealed class SchedulingResource
{
    public string Key { get; set; } = string.Empty;
}
