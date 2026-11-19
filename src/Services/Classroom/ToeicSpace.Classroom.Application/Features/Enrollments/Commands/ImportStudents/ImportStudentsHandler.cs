using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Enrollments;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.EnrollStudent;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.ImportStudents;

public sealed class ImportStudentsHandler(IClassRepository classes, IServiceScopeFactory scopeFactory,
    ILogger<ImportStudentsHandler> logger) : IRequestHandler<ImportStudentsCommand, ImportStudentsResult>
{
    public async Task<ImportStudentsResult> Handle(ImportStudentsCommand request, CancellationToken ct)
    {
        var classroom = await classes.GetByIdAsync(request.ClassId, false, ct) ?? throw ClassRuleException.NotFound();
        if (!classroom.CanManageEnrollments()) throw ClassRuleException.InvalidState();

        var results = new List<ImportStudentRowResult>(request.Rows.Count);
        foreach (var row in request.Rows)
        {
            ct.ThrowIfCancellationRequested();
            Guid? studentId = null;
            if (row.ErrorCode is not null)
            {
                results.Add(Failed(row, null, row.ErrorCode, "Student identifier is missing."));
                continue;
            }
            if (row.StudentId is not null && (!Guid.TryParse(row.StudentId, out var id) || id == Guid.Empty))
            {
                results.Add(Failed(row, null, "invalid_student_id", "Student ID must be a non-empty GUID."));
                continue;
            }
            if (row.Email is not null && (!System.Net.Mail.MailAddress.TryCreate(row.Email, out var address)
                || !string.Equals(address.Address, row.Email, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(Failed(row, null, "invalid_email", "Email address is invalid."));
                continue;
            }

            // Each row gets a fresh DbContext so a failed SaveChanges cannot affect subsequent rows.
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                if (row.Email is not null)
                {
                    var student = await scope.ServiceProvider.GetRequiredService<IStudentReadRepository>()
                        .GetByEmailAsync(row.Email, ct);
                    if (student is null)
                    {
                        results.Add(Failed(row, null, "student_not_synchronized", "Student data is not synchronized yet. Retry later."));
                        continue;
                    }
                    studentId = student.StudentId;
                }
                else if (Guid.TryParse(row.StudentId, out var parsedId))
                    studentId = parsedId;

                if (studentId is null)
                {
                    results.Add(Failed(row, null, "missing_student", "Student identifier is missing."));
                    continue;
                }
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var enrollment = await sender.Send(new EnrollStudentCommand(studentId.Value)
                    { ClassId = request.ClassId, AdminId = request.AdminId }, ct);
                results.Add(new ImportStudentRowResult(row.RowNumber, studentId, enrollment.Id, null, null));
            }
            catch (ClassRuleException ex)
            {
                results.Add(Failed(row, studentId, ex.Code, ex.Message));
            }
        }
        var succeeded = results.Count(x => x.EnrollmentId is not null);
        logger.LogInformation("Processed enrollment batch for class {ClassId} by {AdminId}: {EnrolledCount} succeeded, {FailedCount} failed",
            request.ClassId, request.AdminId, succeeded, results.Count - succeeded);
        return new ImportStudentsResult(results.Count, succeeded, results.Count - succeeded, results);
    }

    private ImportStudentRowResult Failed(EnrollmentImportRow row, Guid? studentId, string code, string message)
    {
        logger.LogDebug("Enrollment batch entry {EntryNumber} failed: {Code}", row.RowNumber, code);
        return new ImportStudentRowResult(row.RowNumber, studentId, null, code, message);
    }
}
