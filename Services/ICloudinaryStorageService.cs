namespace FilmHub.Services;

public interface ICloudinaryStorageService
{
    Task<string?> UploadPosterAsync(IFormFile? file);
}