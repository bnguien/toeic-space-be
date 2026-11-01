using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Interfaces.Repositories;

public interface IStudentReadRepository
{
    Task<StudentReadModel?> GetByIdAsync(Guid studentId, CancellationToken cancellationToken);
    Task<StudentReadModel?> GetByEmailAsync(string email, CancellationToken cancellationToken);
}
