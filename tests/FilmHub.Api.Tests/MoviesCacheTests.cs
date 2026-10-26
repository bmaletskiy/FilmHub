using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FilmHub.Contracts.Movies;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace FilmHub.Api.Tests;

public sealed class MoviesCacheTests : IDisposable
{
    private readonly FilmHubFactory factory = new();
    private readonly HttpClient client;
    private readonly ITestOutputHelper output;

    public MoviesCacheTests(ITestOutputHelper output)
    {
        this.output = output;
        client = factory.CreateSeededClient();
    }

    private static object Movie(string title = "B9 updated") => new
    {
        title, description = "Cache verification overview", releaseYear = 2026,
        posterUrl = (string?)null, genreIds = new[] { 2 }
    };

    [Theory]
    [InlineData("/api/movies")]
    [InlineData("/api/movies/1")]
    public async Task Demonstration_measures_uncached_cold_and_warm_queries(string path)
    {
        // Authorization bypasses the default OutputCache policy; the endpoint remains public.
        async Task<(string Body, long Queries, double Milliseconds, bool Age)> Measure(bool bypass)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (bypass) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "cache-demo");
            var before = factory.Queries.Count;
            var timer = Stopwatch.StartNew();
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            timer.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (body, factory.Queries.Count - before, timer.Elapsed.TotalMilliseconds, response.Headers.Age.HasValue);
        }

        var uncached = await Measure(true);
        var uncachedAgain = await Measure(true);
        var cold = await Measure(false);
        var warm = await Measure(false);
        Assert.True(uncached.Queries > 0 && uncachedAgain.Queries > 0);
        Assert.True(cold.Queries > 0);
        Assert.Equal(0, warm.Queries);
        Assert.False(cold.Age);
        Assert.True(warm.Age);
        Assert.Equal(cold.Body, warm.Body);
        Assert.Equal(uncached.Body, warm.Body);
        output.WriteLine($"{path}: uncached={uncached.Queries} SQL/{uncached.Milliseconds:F3} ms; " +
            $"uncached repeat={uncachedAgain.Queries} SQL/{uncachedAgain.Milliseconds:F3} ms; " +
            $"cold={cold.Queries} SQL/{cold.Milliseconds:F3} ms; warm={warm.Queries} SQL/{warm.Milliseconds:F3} ms.");
        output.WriteLine("Elapsed times are observed on this test machine; no speed ratio is asserted.");
    }

    [Fact]
    public async Task All_query_parameters_vary_cache_for_future_pagination_and_filters()
    {
        // B1 is not implemented yet: bodies currently match, but keys must already be distinct.
        foreach (var path in new[]
        {
            "/api/movies", "/api/movies?page=1&pageSize=10", "/api/movies?page=2&pageSize=10",
            "/api/movies?page=1&pageSize=20", "/api/movies?page=1&pageSize=10&genreId=2",
            "/api/movies?page=1&pageSize=10&sort=title", "/api/movies/1?view=summary"
        })
        {
            var before = factory.Queries.Count;
            using var cold = await client.GetAsync(path);
            Assert.True(factory.Queries.Count > before);
            var afterCold = factory.Queries.Count;
            using var warm = await client.GetAsync(path);
            Assert.Equal(afterCold, factory.Queries.Count);
            Assert.True(warm.Headers.Age.HasValue);
        }
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Successful_CRUD_invalidates_all_list_variants_and_movie_details(string method)
    {
        var paths = new[] { "/api/movies", "/api/movies?page=1&pageSize=10", "/api/movies?page=2&pageSize=10", "/api/movies/1" };
        foreach (var path in paths) await client.GetStringAsync(path);

        using var write = method switch
        {
            "POST" => await client.PostAsJsonAsync("/api/movies", Movie()),
            "PUT" => await client.PutAsJsonAsync("/api/movies/1", Movie()),
            _ => await client.DeleteAsync("/api/movies/1")
        };
        Assert.Equal(method == "POST" ? HttpStatusCode.Created : HttpStatusCode.NoContent, write.StatusCode);
        foreach (var path in paths)
        {
            var before = factory.Queries.Count;
            using var fresh = await client.GetAsync(path);
            var body = await fresh.Content.ReadAsStringAsync();
            Assert.True(factory.Queries.Count > before);
            Assert.False(fresh.Headers.Age.HasValue);
            if (method == "DELETE")
            {
                if (path == "/api/movies/1") Assert.Equal(HttpStatusCode.NotFound, fresh.StatusCode);
                else Assert.Equal("[]", body);
            }
            else if (method == "PUT" || path != "/api/movies/1") Assert.Contains("B9 updated", body);
        }
        if (method == "PUT")
        {
            var movie = await client.GetFromJsonAsync<MovieResponse>("/api/movies/1");
            Assert.Equal(2, Assert.Single(movie!.Genres).Id);
        }
        if (method == "POST")
        {
            var created = (await write.Content.ReadFromJsonAsync<MovieResponse>())!;
            Assert.Equal("B9 updated", (await client.GetFromJsonAsync<MovieResponse>($"/api/movies/{created.Id}"))!.Title);
        }
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Rejected_mutations_do_not_evict_good_responses(string method)
    {
        await client.GetStringAsync("/api/movies");
        await client.GetStringAsync("/api/movies/1");
        using var rejected = method switch
        {
            "POST" => await client.PostAsJsonAsync("/api/movies", new { title = "" }),
            "PUT" => await client.PutAsJsonAsync("/api/movies/1", new
            { title = "Rejected", description = "Invalid genre", releaseYear = 2026, genreIds = new[] { 999 } }),
            _ => await client.DeleteAsync("/api/movies/999")
        };
        Assert.Equal(method == "DELETE" ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest, rejected.StatusCode);
        var before = factory.Queries.Count;
        Assert.Contains("Original", await client.GetStringAsync("/api/movies"));
        Assert.Contains("Original", await client.GetStringAsync("/api/movies/1"));
        Assert.Equal(before, factory.Queries.Count);
    }

    [Fact]
    public async Task Not_found_responses_are_not_cached()
    {
        using var first = await client.GetAsync("/api/movies/999");
        var before = factory.Queries.Count;
        using var second = await client.GetAsync("/api/movies/999");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.True(factory.Queries.Count > before);
        Assert.False(second.Headers.Age.HasValue);
    }

    [Theory]
    [InlineData("movie-sync")]
    [InlineData("genre-async")]
    [InlineData("join-async")]
    public async Task Shared_SaveChanges_path_invalidates_for_MVC_imports_and_genres(string change)
    {
        await client.GetStringAsync("/api/movies");
        await client.GetStringAsync("/api/movies/1");
        await factory.ReadDatabase(async db =>
        {
            if (change == "movie-sync")
            {
                (await db.Movies.SingleAsync()).Title = "Changed through shared context";
                return db.SaveChanges();
            }
            if (change == "genre-async") (await db.Genres.SingleAsync(g => g.Id == 1)).Name = "Renamed genre";
            else
            {
                var movie = await db.Movies.Include(m => m.Genres).SingleAsync();
                movie.Genres.Clear();
                movie.Genres.Add(await db.Genres.SingleAsync(g => g.Id == 2));
            }
            return await db.SaveChangesAsync();
        });
        var before = factory.Queries.Count;
        var updated = (await client.GetFromJsonAsync<MovieResponse>("/api/movies/1"))!;
        Assert.True(factory.Queries.Count > before);
        if (change == "movie-sync") Assert.Equal("Changed through shared context", updated.Title);
        else if (change == "genre-async") Assert.Equal("Renamed genre", Assert.Single(updated.Genres).Name);
        else Assert.Equal(2, Assert.Single(updated.Genres).Id);
        before = factory.Queries.Count;
        await client.GetStringAsync("/api/movies");
        Assert.True(factory.Queries.Count > before);
    }

    [Fact]
    public async Task Failed_database_save_does_not_invalidate_or_publish_unsaved_changes()
    {
        await client.GetStringAsync("/api/movies/1");
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.ReadDatabase(async db =>
        {
            (await db.Movies.SingleAsync()).Title = null!;
            return await db.SaveChangesAsync();
        }));
        var before = factory.Queries.Count;
        Assert.Equal("Original", (await client.GetFromJsonAsync<MovieResponse>("/api/movies/1"))!.Title);
        Assert.Equal(before, factory.Queries.Count);
    }

    [Fact]
    public async Task Inflight_old_GET_cannot_repopulate_a_current_cache_key_after_PUT()
    {
        factory.Pause.Arm();
        var oldRequest = client.GetAsync("/api/movies");
        try
        {
            await factory.Pause.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var write = await client.PutAsJsonAsync("/api/movies/1", Movie("After concurrent write"));
            Assert.Equal(HttpStatusCode.NoContent, write.StatusCode);
        }
        finally { factory.Pause.Release.TrySetResult(); }
        using var old = await oldRequest;
        Assert.Contains("Original", await old.Content.ReadAsStringAsync());
        var before = factory.Queries.Count;
        var fresh = await client.GetStringAsync("/api/movies");
        Assert.Contains("After concurrent write", fresh);
        Assert.DoesNotContain("Original", fresh);
        Assert.True(factory.Queries.Count > before);
        before = factory.Queries.Count;
        Assert.Equal(fresh, await client.GetStringAsync("/api/movies"));
        Assert.Equal(before, factory.Queries.Count);
    }

    public void Dispose() { client.Dispose(); factory.Dispose(); }
}
