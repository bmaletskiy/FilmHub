using System.Globalization;
using Microsoft.AspNetCore.OutputCaching;

namespace FilmHub.Caching;

public sealed class MovieApiCache(IOutputCacheStore store)
{
    public const string Policy = "MoviesApi";
    public const string Tag = "movies-api";
    private long version;

    public string Version => Interlocked.Read(ref version).ToString(CultureInfo.InvariantCulture);

    public async ValueTask InvalidateAsync()
    {
        // Advance before eviction. An already-running GET can only populate an old key,
        // which subsequent requests will never use, even if it completes after eviction.
        Interlocked.Increment(ref version);
        // The database commit has succeeded: finish invalidation even if the client disconnects.
        await store.EvictByTagAsync(Tag, CancellationToken.None);
    }
}
