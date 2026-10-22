using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.DTOs.Classes;

public sealed record ClassDetailDto(ClassDto Class, IReadOnlyList<InstructorDto> InstructorHistory);
