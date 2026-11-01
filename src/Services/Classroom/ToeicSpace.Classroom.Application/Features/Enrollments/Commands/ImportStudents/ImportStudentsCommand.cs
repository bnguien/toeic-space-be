using ToeicSpace.Classroom.Application.DTOs.Enrollments;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.ImportStudents;

public sealed record ImportStudentsCommand(Guid ClassId, Guid AdminId,
    IReadOnlyList<EnrollmentImportRow> Rows) : IRequest<ImportStudentsResult>;
