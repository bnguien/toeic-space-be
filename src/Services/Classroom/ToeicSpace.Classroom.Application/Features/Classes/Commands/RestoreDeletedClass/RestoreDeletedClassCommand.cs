using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.RestoreDeletedClass;

public sealed record RestoreDeletedClassCommand(Guid ClassId, Guid AdminId) : IRequest<ClassDetailDto>;
