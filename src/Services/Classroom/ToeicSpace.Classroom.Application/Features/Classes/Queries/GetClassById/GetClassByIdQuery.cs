using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClassById;

public sealed record GetClassByIdQuery(Guid ClassId, Guid? TeacherId) : IRequest<ClassDetailDto>;
