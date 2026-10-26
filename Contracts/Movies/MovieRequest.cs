using System.ComponentModel.DataAnnotations;

namespace FilmHub.Contracts.Movies;

// PUT replaces all editable fields. IDs, reviews and navigation objects cannot be bound.
public sealed class MovieRequest : IValidatableObject
{
    [Required, StringLength(200)]
    public string Title { get; init; } = null!;

    [Required, StringLength(10000)]
    public string Description { get; init; } = null!;

    [Range(1888, 2100)]
    public int ReleaseYear { get; init; }

    [StringLength(2048)]
    public string? PosterUrl { get; init; }

    [Required, MinLength(1), MaxLength(100)]
    public int[] GenreIds { get; init; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GenreIds is not null && GenreIds.Any(id => id <= 0))
            yield return new ValidationResult("Genre IDs must be positive integers.", [nameof(GenreIds)]);

        if (string.IsNullOrWhiteSpace(PosterUrl)) yield break;
        var poster = PosterUrl.Trim();
        var local = poster.StartsWith('/') && !poster.StartsWith("//")
            && !poster.Contains('\\') && !poster.Any(char.IsControl);
        var external = Uri.TryCreate(poster, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo);
        if (!local && !external)
            yield return new ValidationResult("Poster URL must be an HTTP(S) URL without credentials or a site-relative path starting with a single slash.", [nameof(PosterUrl)]);
    }
}
