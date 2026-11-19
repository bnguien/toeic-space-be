using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class DictationImportRepository(ContentDbContext db) : IDictationImportRepository
{
    public Task<List<DictationCourse>> GetCoursesForUpdateAsync(string pack, IReadOnlyCollection<string> codes,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.DictationCourses.Include(x => x.Items)
                .Where(x => x.Pack == pack || codes.Contains(x.Code))
                .ToListAsync(ct));

    public async Task<List<DictationItem>> GetItemsForUpdateAsync(IReadOnlyCollection<string> sourceCodes,
        CancellationToken ct)
    {
        // Chunked: a TOEIC pack has ~3 000 sentence codes.
        var result = new List<DictationItem>(sourceCodes.Count);
        foreach (var chunk in sourceCodes.Chunk(1000))
            result.AddRange(await PersistenceExceptionTranslator.ExecuteAsync(() =>
                db.DictationItems.Where(x => chunk.Contains(x.SourceCode)).ToListAsync(ct)));
        return result;
    }

    public void AddCourse(DictationCourse course) => db.DictationCourses.Add(course);

    public void AddItem(DictationItem item) => db.DictationItems.Add(item);
}
