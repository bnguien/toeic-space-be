using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyEnrolledClassById;

public sealed record GetMyEnrolledClassByIdQuery(Guid StudentId, Guid ClassId) : IRequest<ClassDetailDto>;
