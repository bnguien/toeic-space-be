using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;

public sealed class TeacherReadRepository(ClassroomDbContext db) : ITeacherReadRepository
{
    public Task<TeacherReadModel?> GetByIdAsync(Guid teacherId, CancellationToken cancellationToken) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.Teachers.AsNoTracking().FirstOrDefaultAsync(x => x.TeacherId == teacherId, cancellationToken));
}
