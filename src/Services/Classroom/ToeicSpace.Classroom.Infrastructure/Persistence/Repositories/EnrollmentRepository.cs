using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;

public sealed class EnrollmentRepository(ClassroomDbContext db) : IEnrollmentRepository
{
    public Task<bool> HasActiveAsync(Guid classId, Guid studentId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.ClassEnrollments.AnyAsync(x => x.ClassId == classId
            && x.StudentId == studentId && x.Status == EnrollmentStatus.Active && x.WithdrawnAt == null, ct));

    public Task<bool> HasHistoryAsync(Guid classId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.ClassEnrollments.AnyAsync(x => x.ClassId == classId, ct));

    public Task<ClassEnrollment?> GetByIdAsync(Guid classId, Guid enrollmentId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.ClassEnrollments
            .FirstOrDefaultAsync(x => x.Id == enrollmentId && x.ClassId == classId, ct));

    public Task<PagedResult<EnrollmentWithStudent>> ListAsync(Guid classId, EnrollmentStatus? status,
        Guid? studentId, int page, int pageSize, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            var query = db.ClassEnrollments.AsNoTracking().Where(x => x.ClassId == classId);
            if (status is EnrollmentStatus.Active)
                query = query.Where(x => x.Status == EnrollmentStatus.Active && x.WithdrawnAt == null);
            else if (status is EnrollmentStatus.Withdrawn)
                query = query.Where(x => x.Status == EnrollmentStatus.Withdrawn);
            if (studentId is { } student) query = query.Where(x => x.StudentId == student);
            var count = await query.CountAsync(ct);
            var enrollments = await query.OrderByDescending(x => x.EnrolledAt).ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            var ids = enrollments.Select(x => x.StudentId).Distinct().ToArray();
            var students = await db.Students.AsNoTracking().Where(x => ids.Contains(x.StudentId))
                .ToDictionaryAsync(x => x.StudentId, ct);
            var items = enrollments.Select(x =>
            {
                students.TryGetValue(x.StudentId, out var projection);
                return new EnrollmentWithStudent(x, projection?.Name, projection?.Email);
            }).ToArray();
            return new PagedResult<EnrollmentWithStudent>(items, page, pageSize, count);
        });

    public void Add(ClassEnrollment enrollment) => db.ClassEnrollments.Add(enrollment);
}
