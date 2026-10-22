using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Interfaces.Repositories;

public interface ITeacherReadRepository
{
    Task<TeacherReadModel?> GetByIdAsync(Guid teacherId, CancellationToken cancellationToken);
}
