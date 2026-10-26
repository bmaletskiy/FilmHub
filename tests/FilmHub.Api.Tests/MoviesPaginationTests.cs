using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FilmHub.Contracts.Movies;
using FilmHub.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FilmHub.Api.Tests;

public sealed class MoviesPaginationTests : IDisposable
{
    private readonly FilmHubFactory factory = new();
    private readonly HttpClient client;

    public MoviesPaginationTests() => client = factory.CreateSeededClient();

    private Task SeedMore() => factory.ReadDatabase(async db =>
    {
        var genres = await db.Genres.ToListAsync();
        foreach (var id in new[] { 9, 3, 7, 5 })
            db.Movies.Add(new Movie
            {
                Id = id, Title = "Same title", Description = "Overview", ReleaseYear = 2026,
                Genres = genres.ToList()
            });
        return await db.SaveChangesAsync();
    });

    private async Task<MoviePageResponse> Page(string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MoviePageResponse>())!;
    }

    private static void AssertNextLink(string? link, int skip, int limit)
    {
        Assert.NotNull(link);
        Assert.StartsWith("/api/movies?", link);
        var query = QueryHelpers.ParseQuery(new Uri(new Uri("https://localhost"), link).Query);
        Assert.Equal(skip.ToString(), query["skip"].ToString());
        Assert.Equal(limit.ToString(), query["limit"].ToString());
    }

    [Fact]
    public async Task Legacy_clients_still_receive_full_sorted_array_without_pagination_parameters()
    {
        await SeedMore();
        foreach (var path in new[] { "/api/movies", "/api/movies?demo=legacy", "/api/movies?page=2&pageSize=1" })
        {
            var movies = (await client.GetFromJsonAsync<MovieResponse[]>(path))!;
            Assert.Equal(new[] { 1, 3, 5, 7, 9 }, movies.Select(m => m.Id));
        }
    }

    [Fact]
    public async Task Pages_have_stable_ID_order_total_and_complete_genres_and_followable_nextLink()
    {
        await SeedMore();
        var first = await Page("/api/movies?skip=0&limit=2");
        Assert.Equal(new[] { 1, 3 }, first.Items.Select(m => m.Id));
        Assert.Equal(5, first.TotalCount);
        Assert.Equal(0, first.Skip);
        Assert.Equal(2, first.Limit);
        Assert.Equal(2, first.Items[1].Genres.Count);
        AssertNextLink(first.NextLink, 2, 2);
        var second = await Page(first.NextLink!);
        Assert.Equal(new[] { 5, 7 }, second.Items.Select(m => m.Id));
        Assert.Equal(5, second.TotalCount);
        Assert.Equal(2, second.Skip);
        var last = await Page(second.NextLink!);
        Assert.Equal(9, Assert.Single(last.Items).Id);
        Assert.Equal(5, last.TotalCount);
        Assert.Null(last.NextLink);
    }

    [Fact]
    public async Task Following_nextLink_visits_every_movie_once()
    {
        await SeedMore();
        string? next = "/api/movies?limit=1";
        var ids = new List<int>();
        while (next is not null)
        {
            Assert.True(ids.Count < 10, "nextLink must eventually terminate.");
            var page = await Page(next);
            Assert.Equal(5, page.TotalCount);
            ids.Add(Assert.Single(page.Items).Id);
            next = page.NextLink;
        }
        Assert.Equal(new[] { 1, 3, 5, 7, 9 }, ids);
    }

    [Theory]
    [InlineData("skip=1", 1, 20)]
    [InlineData("limit=2", 0, 2)]
    [InlineData("skip=0", 0, 20)]
    [InlineData("skip=0&limit=100", 0, 100)]
    public async Task Missing_parameter_uses_documented_default(string query, int skip, int limit)
    {
        await SeedMore();
        var page = await Page("/api/movies?" + query);
        Assert.Equal(skip, page.Skip);
        Assert.Equal(limit, page.Limit);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(Math.Min(limit, 5 - skip), page.Items.Count);
    }

    [Fact]
    public async Task Omitted_limit_enforces_default_and_produces_nextLink()
    {
        await factory.ReadDatabase(async db =>
        {
            for (var id = 2; id <= 25; id++)
                db.Movies.Add(new Movie { Id = id, Title = "Movie", Description = "Overview", ReleaseYear = 2026 });
            return await db.SaveChangesAsync();
        });
        var first = await Page("/api/movies?skip=0");
        Assert.Equal(20, first.Items.Count);
        Assert.Equal(25, first.TotalCount);
        var last = await Page(first.NextLink!);
        Assert.Equal(20, last.Skip);
        Assert.Equal(5, last.Items.Count);
        Assert.Null(last.NextLink);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public async Task At_or_beyond_total_returns_empty_envelope_without_overflow(int skip)
    {
        await SeedMore();
        var page = await Page($"/api/movies?skip={skip}&limit=100");
        Assert.Empty(page.Items);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(skip, page.Skip);
        Assert.Null(page.NextLink);
    }

    [Fact]
    public async Task Empty_database_returns_zero_total_and_null_nextLink()
    {
        await factory.ReadDatabase(db => db.Movies.ExecuteDeleteAsync());
        var page = await Page("/api/movies?skip=0&limit=2");
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Null(page.NextLink);
    }

    [Fact]
    public async Task Exact_full_last_page_has_no_nextLink()
    {
        await SeedMore();
        var page = await Page("/api/movies?skip=3&limit=2");
        Assert.Equal(new[] { 7, 9 }, page.Items.Select(m => m.Id));
        Assert.Null(page.NextLink);
    }

    [Fact]
    public async Task NextLink_preserves_other_query_values_and_encodes_them_safely()
    {
        await SeedMore();
        var page = await Page("/api/movies?SKIP=0&LIMIT=2&page=7&demo=take%20one&tag=a%26b&tag=c%3Dd");
        Assert.StartsWith("/api/movies?", page.NextLink);
        var query = QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, page.NextLink!).Query);
        Assert.Equal("2", query["skip"].ToString());
        Assert.Equal("2", query["limit"].ToString());
        Assert.Equal("take one", query["demo"].ToString());
        Assert.Equal("7", query["page"].ToString());
        Assert.Equal(new[] { "a&b", "c=d" }, query["tag"].ToArray());
        Assert.Equal(new[] { 5, 7 }, (await Page(page.NextLink!)).Items.Select(m => m.Id));
    }

    [Theory]
    [InlineData("skip=-1", "skip")]
    [InlineData("skip=abc", "skip")]
    [InlineData("skip=1.5", "skip")]
    [InlineData("skip=2147483648", "skip")]
    [InlineData("skip=", "skip")]
    [InlineData("skip=%20", "skip")]
    [InlineData("skip=0&skip=1", "skip")]
    [InlineData("limit=0", "limit")]
    [InlineData("limit=-1", "limit")]
    [InlineData("limit=101", "limit")]
    [InlineData("limit=abc", "limit")]
    [InlineData("limit=1.5", "limit")]
    [InlineData("limit=2147483648", "limit")]
    [InlineData("limit=", "limit")]
    [InlineData("limit=%20", "limit")]
    [InlineData("limit=1&limit=2", "limit")]
    public async Task Invalid_parameters_return_400_without_SQL_or_cached_error(string query, string key)
    {
        var before = factory.Queries.Count;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await client.GetAsync("/api/movies?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.False(response.Headers.Age.HasValue);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(json.GetProperty("errors").EnumerateObject(), error => error.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
        }
        Assert.Equal(before, factory.Queries.Count);
    }

    [Fact]
    public async Task Pagination_executes_COUNT_and_database_LIMIT_OFFSET_before_materialization()
    {
        await SeedMore();
        var before = factory.Queries.Commands.Count;
        await Page("/api/movies?skip=1&limit=2");
        var sql = factory.Queries.Commands.Skip(before).ToArray();
        Assert.Equal(2, sql.Length);
        Assert.Contains("COUNT(", sql[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql[1]);
        Assert.Contains("LIMIT", sql[1]);
        Assert.Contains("OFFSET", sql[1]);
    }

    [Fact]
    public async Task Each_page_and_limit_and_legacy_array_cache_independently()
    {
        await SeedMore();
        foreach (var path in new[] { "/api/movies?skip=0&limit=2", "/api/movies?skip=2&limit=2", "/api/movies?skip=0&limit=3", "/api/movies" })
        {
            var before = factory.Queries.Count;
            using var cold = await client.GetAsync(path);
            var body = await cold.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, cold.StatusCode);
            Assert.Equal(path.Contains('?') ? 2 : 1, factory.Queries.Count - before);
            Assert.False(cold.Headers.Age.HasValue);
            before = factory.Queries.Count;
            using var warm = await client.GetAsync(path);
            Assert.Equal(body, await warm.Content.ReadAsStringAsync());
            Assert.Equal(before, factory.Queries.Count);
            Assert.True(warm.Headers.Age.HasValue);
        }
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task CRUD_refreshes_total_items_boundaries_and_nextLink_on_all_cached_pages(string method)
    {
        await SeedMore();
        var paths = new[] { "/api/movies?skip=0&limit=2", "/api/movies?skip=2&limit=2", "/api/movies?skip=4&limit=2" };
        foreach (var path in paths) await Page(path);
        await client.GetStringAsync("/api/movies");
        await client.GetStringAsync("/api/movies/1");
        var body = new { title = "B1 changed", description = "Overview", releaseYear = 2026, genreIds = new[] { 2 } };
        using var write = method switch
        {
            "POST" => await client.PostAsJsonAsync("/api/movies", body),
            "PUT" => await client.PutAsJsonAsync("/api/movies/1", body),
            _ => await client.DeleteAsync("/api/movies/1")
        };
        Assert.Equal(method == "POST" ? HttpStatusCode.Created : HttpStatusCode.NoContent, write.StatusCode);
        var pages = new List<MoviePageResponse>();
        foreach (var path in paths)
        {
            var before = factory.Queries.Count;
            using var response = await client.GetAsync(path);
            Assert.False(response.Headers.Age.HasValue);
            Assert.Equal(2, factory.Queries.Count - before);
            var page = (await response.Content.ReadFromJsonAsync<MoviePageResponse>())!;
            Assert.Equal(method == "POST" ? 6 : method == "DELETE" ? 4 : 5, page.TotalCount);
            pages.Add(page);
            before = factory.Queries.Count;
            await Page(path);
            Assert.Equal(before, factory.Queries.Count);
        }
        if (method == "DELETE")
        {
            Assert.Equal(new[] { 3, 5 }, pages[0].Items.Select(m => m.Id));
            Assert.Null(pages[1].NextLink);
            Assert.Empty(pages[2].Items);
        }
        else if (method == "POST") Assert.Equal(2, pages[2].Items.Count);
        else Assert.Equal("B1 changed", pages[0].Items[0].Title);
        AssertNextLink(pages[0].NextLink, 2, 2);
        Assert.Null(pages[2].NextLink);
        var legacy = (await client.GetFromJsonAsync<MovieResponse[]>("/api/movies"))!;
        Assert.Equal(pages[0].TotalCount, legacy.Length);
        using var detail = await client.GetAsync("/api/movies/1");
        Assert.Equal(method == "DELETE" ? HttpStatusCode.NotFound : HttpStatusCode.OK, detail.StatusCode);
        if (method == "PUT") Assert.Contains("B1 changed", await detail.Content.ReadAsStringAsync());
    }

    public void Dispose() { client.Dispose(); factory.Dispose(); }
}
