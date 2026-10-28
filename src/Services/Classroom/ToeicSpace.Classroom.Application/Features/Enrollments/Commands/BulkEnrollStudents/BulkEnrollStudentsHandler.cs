using ToeicSpace.Classroom.Application.DTOs.Enrollments;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.ImportStudents;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.BulkEnrollStudents;

public sealed class BulkEnrollStudentsHandler(ISender sender)
    : IRequestHandler<BulkEnrollStudentsCommand, BulkEnrollStudentsResult>
{
    public async Task<BulkEnrollStudentsResult> Handle(BulkEnrollStudentsCommand request, CancellationToken ct)
    {
        var rows = request.StudentIds.Select((id, index) =>
            new EnrollmentImportRow(index + 1, id.ToString(), null)).ToArray();
        var result = await sender.Send(new ImportStudentsCommand(request.ClassId, request.AdminId, rows), ct);
        return new BulkEnrollStudentsResult(result.TotalRows, result.ImportedCount, result.FailedCount,
            result.Rows.Select(row => new BulkEnrollStudentResult(row.RowNumber - 1,
                request.StudentIds[row.RowNumber - 1], row.EnrollmentId, row.Code, row.Message)).ToArray());
    }
}
