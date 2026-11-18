using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public Center Center { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public FacilityStatus Status { get; set; } = FacilityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static void Validate(string name, int capacity)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 || capacity <= 0)
            throw new ClassRuleException("invalid_room", "Room name is required and capacity must be positive.");
    }
}
