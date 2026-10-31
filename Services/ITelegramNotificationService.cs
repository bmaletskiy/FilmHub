namespace FilmHub.Services;

public interface ITelegramNotificationService
{
    Task SendNewMovieNotificationAsync(string title, int year, string genres);
}