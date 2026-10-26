using System.Data.Common;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FilmHub.Api.Tests;

// Test-only counter: measures executed EF reader commands, not elapsed-time guesses.
public sealed class QueryCounter : DbCommandInterceptor
{
    private long count;
    private readonly ConcurrentQueue<string> commands = new();
    public long Count => Interlocked.Read(ref count);
    public IReadOnlyList<string> Commands => commands.ToArray();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        commands.Enqueue(command.CommandText);
        Interlocked.Increment(ref count);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        commands.Enqueue(command.CommandText);
        Interlocked.Increment(ref count);
        return ValueTask.FromResult(result);
    }
}
