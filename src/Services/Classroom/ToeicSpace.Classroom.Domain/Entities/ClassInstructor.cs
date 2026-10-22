namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class ClassInstructor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public Guid TeacherId { get; set; }
    public DateTime AssignedAt { get; set; }
    public Guid AssignedBy { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public Guid? UnassignedBy { get; set; }
}
