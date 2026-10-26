using FilmHub.Models;

namespace FilmHub.Contracts.Movies;

public sealed record GenreResponse(int Id, string Name);

public sealed record MovieResponse(
    int Id, string Title, string Description, int ReleaseYear, string? PosterUrl,
    IReadOnlyList<GenreResponse> Genres)
{
    public static MovieResponse FromMovie(Movie movie) => new(
        movie.Id, movie.Title, movie.Description, movie.ReleaseYear, movie.PosterUrl,
        movie.Genres.OrderBy(g => g.Name).ThenBy(g => g.Id)
            .Select(g => new GenreResponse(g.Id, g.Name)).ToArray());
}
