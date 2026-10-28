
using System.Net.Http.Json;

namespace FilmHub.Services;

public class TelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TelegramNotificationService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task SendNewMovieNotificationAsync(
        string title,
        int year,
        string genres)
    {
        var token = _configuration["Telegram:BotToken"];
        var chatId = _configuration["Telegram:ChatId"];

        if (string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(chatId))
        {
            return;
        }

        var message =
            $"🎬 New movie added!\n\n" +
            $"Title: {title}\n" +
            $"Year: {year}\n" +
            $"Genres: {genres}";

        var response = await _httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new
            {
                chat_id = chatId,
                text = message
            });

        response.EnsureSuccessStatusCode();
    }
}
