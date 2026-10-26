using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FilmHub.Api.Tests;

// Test-only counter: measures executed EF reader commands, not elapsed-time guesses.
public sealed class QueryCounter : DbCommandInterceptor
{
    private long count;
    public long Count => Interlocked.Read(ref count);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref count);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref count);
        return ValueTask.FromResult(result);
    }
}
