using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.UnassignTeacher;

public sealed record UnassignTeacherCommand(Guid ClassId, Guid AdminId) : IRequest<ClassDetailDto>;
