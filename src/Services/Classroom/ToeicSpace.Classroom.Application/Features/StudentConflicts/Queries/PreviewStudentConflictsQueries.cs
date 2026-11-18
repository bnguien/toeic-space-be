using ToeicSpace.Classroom.Application.DTOs.Scheduling;

namespace ToeicSpace.Classroom.Application.Features.StudentConflicts.Queries;

public sealed record PreviewScheduleCreationQuery(ScheduleInput Input, ConflictDetailsRequest? Details)
    : IRequest<StudentConflictPreview>;

public sealed record PreviewScheduleUpdateQuery(Guid Id, ScheduleInput Input, int ExpectedVersion,
    ConflictDetailsRequest? Details) : IRequest<StudentConflictPreview>;

public sealed record PreviewSessionGenerationQuery(Guid ClassId, GenerateSessionsInput Input,
    ConflictDetailsRequest? Details) : IRequest<StudentConflictPreview>;

public sealed record PreviewSessionCreationQuery(SessionInput Input, Guid Actor, bool IsAdmin,
    ConflictDetailsRequest? Details) : IRequest<StudentConflictPreview>;

public sealed record PreviewSessionUpdateQuery(Guid Id, SessionEdit Input, Guid Actor, bool IsAdmin,
    ConflictDetailsRequest? Details) : IRequest<StudentConflictPreview>;

public sealed record PreviewEnrollmentQuery(Guid ClassId, IReadOnlyList<Guid> StudentIds,
    ConflictDetailsRequest? Details) : IRequest<StudentConflictPreview>;
