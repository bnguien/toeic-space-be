using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class ClassEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public Guid StudentId { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public DateTime EnrolledAt { get; set; }
    public Guid EnrolledBy { get; set; }
    public DateTime? WithdrawnAt { get; set; }
    public Guid? WithdrawnBy { get; set; }

    public bool IsCurrent => Status == EnrollmentStatus.Active && WithdrawnAt is null;
}
