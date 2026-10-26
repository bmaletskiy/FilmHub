namespace FilmHub.Models;

public class StatisticsViewModel
{
    public List<ReleaseYearStatistic> ByReleaseYear { get; set; } = new();
    public List<GenreStatistic> ByGenre { get; set; } = new();
    public int TotalMovies => ByReleaseYear.Sum(item => item.Count);
}

public class ReleaseYearStatistic
{
    public int Year { get; set; }
    public int Count { get; set; }
}

public class GenreStatistic
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
