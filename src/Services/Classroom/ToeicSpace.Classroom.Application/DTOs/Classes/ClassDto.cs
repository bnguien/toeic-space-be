using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.DTOs.Classes;

public sealed record ClassDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    ClassStatus Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTime CreatedAt,
    Guid CreatedBy,
    DateTime UpdatedAt,
    Guid UpdatedBy,
    Guid? TeacherId,
    ClassStatus? ArchivedFromStatus,
    string? TeacherName = null);
