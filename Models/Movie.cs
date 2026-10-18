namespace FilmHub.Models;

public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public string? PosterUrl { get; set; }

    public List<Genre> Genres { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
}