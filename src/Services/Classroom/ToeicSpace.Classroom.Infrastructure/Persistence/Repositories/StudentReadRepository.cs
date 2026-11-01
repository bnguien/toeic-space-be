using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;

public sealed class StudentReadRepository(ClassroomDbContext db) : IStudentReadRepository
{
    public Task<StudentReadModel?> GetByIdAsync(Guid studentId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Students.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudentId == studentId, ct));

    public Task<StudentReadModel?> GetByEmailAsync(string email, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Students.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == email, ct));
}
