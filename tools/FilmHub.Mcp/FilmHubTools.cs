using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;

namespace FilmHub.Mcp;

public record GenreDto(int Id, string Name);
public record MovieDto(int Id, string Title, string Description, int ReleaseYear, string? PosterUrl, List<GenreDto> Genres);

[McpServerToolType]
public static class FilmHubTools
{
    [McpServerTool(Name = "list_movies"),
     Description("Lists movies from FilmHub ordered by id. Supports paging with skip and limit.")]
    public static async Task<string> ListMovies(
        HttpClient http,
        [Description("How many movies to skip")] int skip = 0,
        [Description("How many movies to return, 1-100")] int limit = 20,
        CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"api/movies?skip={skip}&limit={limit}", ct);
        return await ReadOrError(resp, ct);
    }

    [McpServerTool(Name = "get_movie"),
     Description("Gets one movie by its numeric id, with description, year, poster and genres.")]
    public static async Task<string> GetMovie(
        HttpClient http,
        [Description("Movie id")] int id,
        CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"api/movies/{id}", ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return $"Movie {id} not found.";
        return await ReadOrError(resp, ct);
    }

    [McpServerTool(Name = "search_movies"),
     Description("Full-text search (Lucene) over movie titles, descriptions and genres. Most relevant first. Whole words only, English.")]
    public static async Task<string> SearchMovies(
        HttpClient http,
        [Description("Search text, for example: avengers thanos")] string query,
        [Description("Max results, 1-100")] int limit = 10,
        CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"api/movies/search?q={Uri.EscapeDataString(query)}&limit={limit}", ct);
        return await ReadOrError(resp, ct);
    }

    [McpServerTool(Name = "list_genres"),
     Description("Lists genres (id and name) that are used by movies in FilmHub.")]
    public static async Task<string> ListGenres(HttpClient http, CancellationToken ct = default)
    {
        var genres = await LoadGenres(http, ct);
        return string.Join("\n", genres.Select(g => $"{g.Id}: {g.Name}"));
    }

    [McpServerTool(Name = "add_movie"),
     Description("Adds a new movie to FilmHub. Genres are given by name and must already exist (see list_genres).")]
    public static async Task<string> AddMovie(
        HttpClient http,
        [Description("Movie title, up to 200 characters")] string title,
        [Description("Short description")] string description,
        [Description("Release year, 1888-2100")] int releaseYear,
        [Description("Genre names, at least one, e.g. [\"Drama\", \"Sci-Fi\"]")] string[] genres,
        [Description("Optional poster URL (http/https)")] string? posterUrl = null,
        CancellationToken ct = default)
    {
        var known = await LoadGenres(http, ct);
        var ids = new List<int>();
        var unknown = new List<string>();
        foreach (var name in genres)
        {
            var match = known.FirstOrDefault(g => g.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null) unknown.Add(name); else ids.Add(match.Id);
        }
        if (unknown.Count > 0)
            return $"Unknown genres: {string.Join(", ", unknown)}. Available: {string.Join(", ", known.Select(g => g.Name))}.";

        var body = new { title, description, releaseYear, posterUrl, genreIds = ids.Distinct().ToArray() };
        var resp = await http.PostAsJsonAsync("api/movies", body, ct);
        return await ReadOrError(resp, ct);
    }

    private static async Task<List<GenreDto>> LoadGenres(HttpClient http, CancellationToken ct)
    {
        var movies = await http.GetFromJsonAsync<List<MovieDto>>("api/movies", ct) ?? [];
        return movies.SelectMany(m => m.Genres).DistinctBy(g => g.Id).OrderBy(g => g.Name).ToList();
    }

    private static async Task<string> ReadOrError(HttpResponseMessage resp, CancellationToken ct)
    {
        var text = await resp.Content.ReadAsStringAsync(ct);
        return resp.IsSuccessStatusCode ? text : $"Error {(int)resp.StatusCode}: {text}";
    }
}
