using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.HardDeleteClass;

public sealed record HardDeleteClassCommand(Guid ClassId) : IRequest;
