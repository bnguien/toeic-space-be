using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.StudentConflicts.Queries;

public sealed class PreviewScheduleCreationHandler(IStudentConflictPreviewService preview)
    : IRequestHandler<PreviewScheduleCreationQuery, StudentConflictPreview>
{
    public Task<StudentConflictPreview> Handle(PreviewScheduleCreationQuery r, CancellationToken ct) =>
        preview.CreateScheduleAsync(r.Input, r.Details, ct);
}

public sealed class PreviewScheduleUpdateHandler(IStudentConflictPreviewService preview)
    : IRequestHandler<PreviewScheduleUpdateQuery, StudentConflictPreview>
{
    public Task<StudentConflictPreview> Handle(PreviewScheduleUpdateQuery r, CancellationToken ct) =>
        preview.UpdateScheduleAsync(r.Id, r.Input, r.ExpectedVersion, r.Details, ct);
}

public sealed class PreviewSessionGenerationHandler(IStudentConflictPreviewService preview)
    : IRequestHandler<PreviewSessionGenerationQuery, StudentConflictPreview>
{
    public Task<StudentConflictPreview> Handle(PreviewSessionGenerationQuery r, CancellationToken ct) =>
        preview.GenerateSessionsAsync(r.ClassId, r.Input, r.Details, ct);
}

public sealed class PreviewSessionCreationHandler(IStudentConflictPreviewService preview)
    : IRequestHandler<PreviewSessionCreationQuery, StudentConflictPreview>
{
    public Task<StudentConflictPreview> Handle(PreviewSessionCreationQuery r, CancellationToken ct) =>
        preview.CreateSessionAsync(r.Input, r.Actor, r.IsAdmin, r.Details, ct);
}

public sealed class PreviewSessionUpdateHandler(IStudentConflictPreviewService preview)
    : IRequestHandler<PreviewSessionUpdateQuery, StudentConflictPreview>
{
    public Task<StudentConflictPreview> Handle(PreviewSessionUpdateQuery r, CancellationToken ct) =>
        preview.UpdateSessionAsync(r.Id, r.Input, r.Actor, r.IsAdmin, r.Details, ct);
}

public sealed class PreviewEnrollmentHandler(IEnrollmentConflictPreviewService preview)
    : IRequestHandler<PreviewEnrollmentQuery, StudentConflictPreview>
{
    public Task<StudentConflictPreview> Handle(PreviewEnrollmentQuery r, CancellationToken ct) =>
        preview.PreviewAsync(r.ClassId, r.StudentIds, r.Details, ct);
}
