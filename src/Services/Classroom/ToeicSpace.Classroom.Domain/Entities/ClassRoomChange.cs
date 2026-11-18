namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class ClassRoomChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid? PreviousRoomId { get; set; }
    public Guid? NewRoomId { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }
}
