using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class GrammarContentRepository(ContentDbContext db) : IGrammarContentRepository
{
    public async Task<IReadOnlyList<GrammarDocument>> ListAsync(CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarDocuments.AsNoTracking().ToListAsync(ct));

    public async Task<IReadOnlyList<GrammarDocument>> ListForUpdateAsync(CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.GrammarDocuments.ToListAsync(ct));

    public void Add(GrammarDocument document) => db.GrammarDocuments.Add(document);
}
