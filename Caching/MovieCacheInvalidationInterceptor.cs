using FilmHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FilmHub.Caching;

// Scoped with AppDbContext: covers API, MVC and imports without duplicating write logic.
public sealed class MovieCacheInvalidationInterceptor(MovieApiCache cache) : SaveChangesInterceptor
{
    private bool affectsMovieResponses;

    private void Inspect(DbContext? context)
    {
        // Entries() detects changes, including implicit many-to-many join rows.
        affectsMovieResponses = context?.ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && (entry.Entity is Movie or Genre || entry.Metadata.GetTableName() == "MovieGenres")) == true;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Inspect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Inspect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (affectsMovieResponses && result > 0) cache.InvalidateAsync().AsTask().GetAwaiter().GetResult();
        affectsMovieResponses = false;
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (affectsMovieResponses && result > 0) await cache.InvalidateAsync();
        affectsMovieResponses = false;
        return result;
    }
}
