
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace FilmHub.Services;

public class CloudinaryStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryStorageService(IConfiguration configuration)
    {
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(apiSecret))
        {
            throw new InvalidOperationException(
                "Cloudinary configuration is missing.");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string?> UploadPosterAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        await using var stream = file.OpenReadStream();

        var parameters = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "filmhub/posters",
            PublicId = Guid.NewGuid().ToString("N"),
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(parameters);

        if (result.Error != null)
            throw new InvalidOperationException(result.Error.Message);

        return result.SecureUrl?.ToString()
            ?? throw new InvalidOperationException(
                "Cloudinary did not return an image URL.");
    }
}
