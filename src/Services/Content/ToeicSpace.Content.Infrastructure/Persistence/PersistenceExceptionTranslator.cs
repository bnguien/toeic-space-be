using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Infrastructure.Persistence;

internal static class PersistenceExceptionTranslator
{
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (DbUpdateConcurrencyException ex)
        { throw new ContentRuleException("concurrency_conflict", "Course was modified concurrently. Retry.", 409, ex); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        { throw new ContentRuleException("unique_constraint", "An item with this unique key already exists.", 409, ex); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1451 })
        { throw new ContentRuleException("module_in_use", "Module has related content and cannot be deleted.", 409, ex); }
        catch (MySqlException ex) when (ex.Number is 1205 or 1213)
        { throw new ContentRuleException("concurrency_conflict", "Course was modified concurrently. Retry.", 409, ex); }
    }
}
