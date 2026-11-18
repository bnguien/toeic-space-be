using ToeicSpace.Classroom.Application.Common.Pagination;

namespace ToeicSpace.Classroom.Application.DTOs.Scheduling;

public sealed record StudentConflictSession(Guid SessionId, Guid ClassId, DateTime StartAt, DateTime EndAt);

public sealed record StudentConflictDetail(Guid StudentId, StudentConflictSession First, StudentConflictSession Second,
    DateTime OverlapStart, DateTime OverlapEnd);

public sealed record StudentConflictPreview(int AffectedStudentCount, int ConflictCount,
    PagedResult<StudentConflictDetail>? Details = null);

public sealed record ConflictDetailsRequest(int Page = 1, int PageSize = 20);
