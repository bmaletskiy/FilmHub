using System.Net.Http.Headers;
using System.Text.Json;
using FilmHub.Data;
using FilmHub.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class ImportController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public ImportController(
        AppDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<IActionResult> Tmdb()
    {
        var token = _configuration["TMDB_TOKEN"];

        if (string.IsNullOrWhiteSpace(token))
            return Content("TMDB_TOKEN not configured.");

        using var client = new HttpClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        // 1. Genres
        var genreJson = await client.GetStringAsync(
            "https://api.themoviedb.org/3/genre/movie/list?language=en-US");

        using var genreDoc = JsonDocument.Parse(genreJson);

        var genreMap = new Dictionary<int, Genre>();

        foreach (var g in genreDoc.RootElement.GetProperty("genres").EnumerateArray())
        {
            int tmdbId = g.GetProperty("id").GetInt32();
            string name = g.GetProperty("name").GetString()!;

            var genre = await _context.Genres
                .FirstOrDefaultAsync(x => x.Name == name);

            if (genre == null)
            {
                genre = new Genre { Name = name };
                _context.Genres.Add(genre);
                await _context.SaveChangesAsync();
            }

            genreMap[tmdbId] = genre;
        }

        // 2. Two pages = about 40 movies
        for (int page = 1; page <= 5; page++)
        {
            var movieJson = await client.GetStringAsync(
                $"https://api.themoviedb.org/3/movie/popular?language=en-US&page={page}");

            using var movieDoc = JsonDocument.Parse(movieJson);

            foreach (var m in movieDoc.RootElement
                         .GetProperty("results")
                         .EnumerateArray())
            {
                string title = m.GetProperty("title").GetString() ?? "";

                if (string.IsNullOrWhiteSpace(title))
                    continue;

                bool exists = await _context.Movies
                    .AnyAsync(x => x.Title == title);

                if (exists)
                    continue;

                string description =
                    m.GetProperty("overview").GetString() ?? "";

                string releaseDate =
                    m.GetProperty("release_date").GetString() ?? "";

                int releaseYear = 0;

                if (releaseDate.Length >= 4)
                    int.TryParse(releaseDate[..4], out releaseYear);

                string? posterPath = null;

                if (m.TryGetProperty("poster_path", out var posterElement)
                    && posterElement.ValueKind != JsonValueKind.Null)
                {
                    posterPath = posterElement.GetString();
                }

                var movie = new Movie
                {
                    Title = title,
                    Description = description,
                    ReleaseYear = releaseYear,
                    PosterUrl = posterPath == null
                        ? null
                        : $"https://image.tmdb.org/t/p/w500{posterPath}"
                };

                foreach (var genreIdElement in
                         m.GetProperty("genre_ids").EnumerateArray())
                {
                    int genreId = genreIdElement.GetInt32();

                    if (genreMap.TryGetValue(genreId, out var genre))
                        movie.Genres.Add(genre);
                }

                _context.Movies.Add(movie);
            }

            await _context.SaveChangesAsync();
        }

        return Content("TMDB import completed.");
    }
}