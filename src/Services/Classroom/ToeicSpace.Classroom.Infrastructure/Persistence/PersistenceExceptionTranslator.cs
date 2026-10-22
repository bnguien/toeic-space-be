using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Infrastructure.Persistence;

internal static class PersistenceExceptionTranslator
{
    public static async Task ExecuteAsync(Func<Task> action) =>
        await ExecuteAsync(async () => { await action(); return true; });

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ClassRuleException("concurrency_conflict", "Class was modified concurrently. Reload and retry.", 409, ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            throw new ClassRuleException("unique_constraint", "Class code or current teacher conflicts with an existing record.", 409, ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1451 })
        {
            throw new ClassRuleException("delete_blocked", "Class has related data and cannot be permanently deleted.", 409, ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1205 or 1213 })
        {
            throw new ClassRuleException("concurrency_conflict", "Class was modified concurrently. Reload and retry.", 409, ex);
        }
        catch (MySqlException ex) when (ex.Number is 1205 or 1213)
        {
            throw new ClassRuleException("concurrency_conflict", "Class was modified concurrently. Reload and retry.", 409, ex);
        }
    }
}
