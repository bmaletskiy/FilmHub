using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using FilmHub.Contracts.Movies;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FilmHub.Api.Tests;

public sealed class MoviesApiTests : IDisposable
{
    private readonly FilmHubFactory factory = new();
    private readonly HttpClient client;

    public MoviesApiTests() => client = factory.CreateSeededClient();

    private static JsonObject Valid() => new()
    {
        ["title"] = " New movie ", ["description"] = " Overview ", ["releaseYear"] = 2026,
        ["posterUrl"] = "/images/demo.jpg", ["genreIds"] = new JsonArray(1, 2)
    };

    [Fact]
    public async Task List_returns_DTOs_with_genres_without_navigation_cycles()
    {
        var response = await client.GetAsync("/api/movies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
        var movie = Assert.Single(json)!;
        Assert.Equal("Original", movie["title"]!.GetValue<string>());
        Assert.Equal("Drama", movie["genres"]![0]!["name"]!.GetValue<string>());
        Assert.Null(movie["reviews"]);
        Assert.Null(movie["genres"]![0]!["movies"]);
    }

    [Fact]
    public async Task List_returns_empty_array_when_database_is_empty()
    {
        await factory.ReadDatabase(db => db.Movies.ExecuteDeleteAsync());
        Assert.Empty((await client.GetFromJsonAsync<MovieResponse[]>("/api/movies"))!);
    }

    [Fact]
    public async Task Get_returns_one_movie()
    {
        var movie = await client.GetFromJsonAsync<MovieResponse>("/api/movies/1");
        Assert.Equal(1, movie!.Id);
        Assert.Equal("Original overview", movie.Description);
        Assert.Equal(1, Assert.Single(movie.Genres).Id);
    }

    [Theory]
    [InlineData("GET", "/api/movies/999")]
    [InlineData("GET", "/api/movies/0")]
    [InlineData("GET", "/api/movies/-1")]
    [InlineData("GET", "/api/movies/not-an-id")]
    [InlineData("PUT", "/api/movies/999")]
    [InlineData("DELETE", "/api/movies/999")]
    public async Task Missing_movies_return_404(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "PUT") request.Content = JsonContent.Create(Valid());
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Post_persists_trimmed_fields_and_genres_and_returns_followable_location()
    {
        var response = await client.PostAsJsonAsync("/api/movies", Valid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<MovieResponse>())!;
        Assert.True(created.Id > 1);
        Assert.Equal("New movie", created.Title);
        Assert.Equal("Overview", created.Description);
        Assert.Equal(new[] { 2, 1 }, created.Genres.Select(g => g.Id));
        Assert.EndsWith($"/api/movies/{created.Id}", response.Headers.Location!.ToString());
        var stored = await client.GetFromJsonAsync<MovieResponse>(response.Headers.Location);
        Assert.Equal(created.Title, stored!.Title);
        Assert.Equal(2, await factory.ReadDatabase(db => db.Movies.CountAsync()));
        Assert.Equal(2, await factory.ReadDatabase(db => db.Genres.CountAsync()));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task Duplicate_genre_ids_are_deduplicated(string method)
    {
        var body = Valid(); body["genreIds"] = new JsonArray(1, 1, 2, 2);
        var response = await SendWrite(method, body);
        Assert.Equal(method == "POST" ? HttpStatusCode.Created : HttpStatusCode.NoContent, response.StatusCode);
        var id = method == "POST" ? (await response.Content.ReadFromJsonAsync<MovieResponse>())!.Id : 1;
        Assert.Equal(2, (await client.GetFromJsonAsync<MovieResponse>($"/api/movies/{id}"))!.Genres.Count);
    }

    [Fact]
    public async Task Put_replaces_fields_and_genres_without_deleting_reviews_or_old_genres()
    {
        var body = Valid(); body["genreIds"] = new JsonArray(2); body["posterUrl"] = null;
        var response = await client.PutAsJsonAsync("/api/movies/1", body);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        var updated = (await client.GetFromJsonAsync<MovieResponse>("/api/movies/1"))!;
        Assert.Equal("New movie", updated.Title);
        Assert.Equal("Overview", updated.Description);
        Assert.Equal(2026, updated.ReleaseYear);
        Assert.Null(updated.PosterUrl);
        Assert.Equal(2, Assert.Single(updated.Genres).Id);
        Assert.Equal(1, await factory.ReadDatabase(db => db.Reviews.CountAsync()));
        Assert.Equal(2, await factory.ReadDatabase(db => db.Genres.CountAsync()));
    }

    [Fact]
    public async Task Repeated_put_is_idempotent_even_when_genres_overlap()
    {
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/movies/1", Valid())).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<MovieResponse>("/api/movies/1"))!.Genres.Count);
        Assert.Equal(1, await factory.ReadDatabase(db => db.Movies.CountAsync()));
    }

    [Fact]
    public async Task Delete_cascades_reviews_and_links_but_preserves_genres_and_poster_files()
    {
        var response = await client.DeleteAsync("/api/movies/1");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/movies/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/movies/1")).StatusCode);
        Assert.Equal(0, await factory.ReadDatabase(db => db.Reviews.CountAsync()));
        Assert.Equal(0, await factory.ReadDatabase(db => db.Set<Dictionary<string, object>>("GenreMovie").CountAsync()));
        Assert.Equal(2, await factory.ReadDatabase(db => db.Genres.CountAsync()));
    }

    public static IEnumerable<object[]> InvalidBodies()
    {
        foreach (var method in new[] { "POST", "PUT" })
        foreach (var (field, value) in new (string, JsonNode?)[]
        {
            ("title", null), ("title", JsonValue.Create("   ")), ("title", JsonValue.Create(new string('a', 201))),
            ("description", null), ("description", JsonValue.Create(" ")), ("description", JsonValue.Create(new string('a', 10001))),
            ("releaseYear", JsonValue.Create(1887)), ("releaseYear", JsonValue.Create(2101)),
            ("genreIds", null), ("genreIds", new JsonArray()), ("genreIds", new JsonArray(0)),
            ("genreIds", new JsonArray(-1)), ("genreIds", new JsonArray(1, 999)),
            ("genreIds", new JsonArray(Enumerable.Range(1, 101).Select(n => (JsonNode?)JsonValue.Create(n)).ToArray())),
            ("posterUrl", JsonValue.Create("javascript:alert(1)")), ("posterUrl", JsonValue.Create("//evil.example/a.jpg")),
            ("posterUrl", JsonValue.Create("https://user:password@example.com/a.jpg")),
            ("posterUrl", JsonValue.Create(new string('a', 2049)))
        })
        {
            var body = Valid(); body[field] = value?.DeepClone();
            yield return new object[] { method, field, body.ToJsonString() };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Invalid_input_returns_validation_problem_and_changes_nothing(string method, string field, string json)
    {
        var response = await SendWrite(method, JsonNode.Parse(json)!);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(400, problem["status"]!.GetValue<int>());
        Assert.Contains(problem["errors"]!.AsObject(), p => string.Equals(p.Key, field, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, await factory.ReadDatabase(db => db.Movies.CountAsync()));
        var original = (await client.GetFromJsonAsync<MovieResponse>("/api/movies/1"))!;
        Assert.Equal("Original", original.Title);
        Assert.Equal(1, Assert.Single(original.Genres).Id);
        Assert.Equal("/images/original.jpg", original.PosterUrl);
    }

    [Theory]
    [InlineData("POST", "null")]
    [InlineData("POST", "{}")]
    [InlineData("POST", "{broken")]
    [InlineData("PUT", "null")]
    [InlineData("PUT", "{}")]
    [InlineData("PUT", "{broken")]
    public async Task Invalid_JSON_or_missing_fields_returns_400(string method, string json)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), method == "POST" ? "/api/movies" : "/api/movies/1")
        { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(1, await factory.ReadDatabase(db => db.Movies.CountAsync()));
    }

    [Theory]
    [InlineData("https://example.com/poster.jpg")]
    [InlineData("http://example.com/poster.jpg")]
    [InlineData("/images/poster.jpg")]
    [InlineData(null)]
    public async Task Valid_poster_values_are_accepted(string? poster)
    {
        var body = Valid(); body["posterUrl"] = poster;
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/movies", body)).StatusCode);
    }

    [Fact]
    public async Task Overposting_cannot_change_ID_reviews_or_create_nested_genres()
    {
        var body = Valid(); body["id"] = 999; body["reviews"] = new JsonArray(new JsonObject { ["rating"] = 10 });
        body["genres"] = new JsonArray(new JsonObject { ["id"] = 999, ["name"] = "Injected" });
        var response = await client.PostAsJsonAsync("/api/movies", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(999, (await response.Content.ReadFromJsonAsync<MovieResponse>())!.Id);
        Assert.Equal(1, await factory.ReadDatabase(db => db.Reviews.CountAsync()));
        Assert.Equal(2, await factory.ReadDatabase(db => db.Genres.CountAsync()));
    }

    [Fact]
    public async Task Unsupported_content_type_returns_415()
    {
        var response = await client.PostAsync("/api/movies", new StringContent("movie", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task API_changes_are_visible_to_MVC_F1_and_C7_and_F2_is_preserved()
    {
        var createdResponse = await client.PostAsJsonAsync("/api/movies", Valid());
        var created = (await createdResponse.Content.ReadFromJsonAsync<MovieResponse>())!;
        var html = await client.GetStringAsync($"/Movies/Details/{created.Id}?utm_source=c5-test");
        Assert.Contains("New movie", html);
        Assert.Contains("name=\"description\"", html);
        Assert.Contains("rel=\"canonical\"", html);
        Assert.Contains("property=\"og:title\"", html);
        Assert.Contains("googletagmanager.com/gtag/js?id=G-W07P4T2NPZ", html);
        var sitemap = XDocument.Parse(await client.GetStringAsync("/sitemaps/movies-1.xml"));
        Assert.Contains(sitemap.Descendants(), e => e.Name.LocalName == "loc" && e.Value.EndsWith($"/Movies/Details/{created.Id}"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Statistics")).StatusCode);
        Assert.Contains("js-genre-select", await client.GetStringAsync("/Movies/Create"));
        Assert.Contains("js-genre-select", await client.GetStringAsync("/Movies/Edit/1"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Movies/Delete/1")).StatusCode);
        var shortSearch = await client.GetFromJsonAsync<JsonObject>("/Genres/Search?term=ab");
        Assert.Empty(shortSearch!["results"]!.AsArray());
        Assert.Contains("Sitemap:", await client.GetStringAsync("/robots.txt"));
    }

    private Task<HttpResponseMessage> SendWrite(string method, JsonNode body) => client.SendAsync(new HttpRequestMessage(
        new HttpMethod(method), method == "POST" ? "/api/movies" : "/api/movies/1") { Content = JsonContent.Create(body) });

    public void Dispose() { client.Dispose(); factory.Dispose(); }
}
