using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.StudentConflicts.Queries;

public sealed record GetMyStudentConflictsQuery(Guid StudentId, DateTime From, DateTime To,
    ConflictDetailsRequest? Details) : IRequest<StudentConflictPreview>;

public sealed class GetMyStudentConflictsHandler(IStudentConflictReadService conflicts)
    : IRequestHandler<GetMyStudentConflictsQuery, StudentConflictPreview>
{
    public Task<StudentConflictPreview> Handle(GetMyStudentConflictsQuery r, CancellationToken ct) =>
        conflicts.GetMineAsync(r.StudentId, r.From, r.To, r.Details, ct);
}
