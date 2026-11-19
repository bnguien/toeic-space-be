using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.DTOs.Classes;

public sealed record InstructorDto(
    Guid Id,
    Guid TeacherId,
    DateTime AssignedAt,
    Guid AssignedBy,
    DateTime? UnassignedAt,
    Guid? UnassignedBy);
