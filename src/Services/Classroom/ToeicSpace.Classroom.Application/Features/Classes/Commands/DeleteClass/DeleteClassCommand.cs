using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.DeleteClass;

public sealed record DeleteClassCommand(Guid ClassId, Guid AdminId) : IRequest;
