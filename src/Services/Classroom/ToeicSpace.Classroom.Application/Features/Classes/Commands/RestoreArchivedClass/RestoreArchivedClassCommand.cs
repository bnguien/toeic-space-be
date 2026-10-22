using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.RestoreArchivedClass;

public sealed record RestoreArchivedClassCommand(Guid ClassId, Guid AdminId) : IRequest<ClassDetailDto>;
