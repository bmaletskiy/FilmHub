namespace FilmHub.Models;

public class Review
{
    public int Id { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int Rating { get; set; }

    public int MovieId { get; set; }
    public Movie? Movie { get; set; }
}