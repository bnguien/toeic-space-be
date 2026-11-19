using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface ILessonRepository
{
    Task<IReadOnlyList<Lesson>> ListAsync(Guid moduleId, bool tracking, CancellationToken ct);
    Task<Lesson?> GetAsync(Guid moduleId, Guid lessonId, CancellationToken ct);
    Task<bool> IsObjectKeyUsedAsync(string key, CancellationToken ct);
    Task LockAsync(Guid lessonId, CancellationToken ct);
    void AddVideo(LessonVideo video);
    void RemoveVideo(LessonVideo video);
    Task ShiftOrdersAsync(Guid moduleId, int offset, CancellationToken ct);
    void Add(Lesson lesson);
    void Remove(Lesson lesson);
    void AddResource(LessonResource resource);
    void RemoveResource(LessonResource resource);
}
