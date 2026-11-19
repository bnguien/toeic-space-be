using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface IDictationImportRepository
{
    /// <summary>Tracked courses of the pack or with one of the codes (active or not), with all their sentences.</summary>
    Task<List<DictationCourse>> GetCoursesForUpdateAsync(string pack, IReadOnlyCollection<string> codes,
        CancellationToken ct);
    /// <summary>Tracked sentences with one of the source codes, wherever they are.</summary>
    Task<List<DictationItem>> GetItemsForUpdateAsync(IReadOnlyCollection<string> sourceCodes, CancellationToken ct);
    void AddCourse(DictationCourse course);
    void AddItem(DictationItem item);
}
