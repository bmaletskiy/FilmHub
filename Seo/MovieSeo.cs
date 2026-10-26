using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using FilmHub.Models;

namespace FilmHub.Seo;

public static class SeoUrls
{
    public static Uri BaseUri(IConfiguration configuration)
    {
        var value = configuration["Seo:PublicBaseUrl"] ?? "http://13.60.182.225";
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https")
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("Seo:PublicBaseUrl must be an absolute HTTP(S) site URL without credentials, query or fragment.");
        return uri;
    }

    public static string Absolute(IConfiguration configuration, string path) =>
        new Uri(BaseUri(configuration), path.TrimStart('/')).AbsoluteUri;

    public static string Movie(IConfiguration configuration, int id) =>
        Absolute(configuration, $"Movies/Details/{id}");
}

public sealed record MovieSeo(string Title, string Description, string CanonicalUrl, string ImageUrl, string ImageAlt)
{
    public static MovieSeo Create(Movie movie, IConfiguration configuration)
    {
        var name = Plain(movie.Title);
        if (name.Length == 0) name = "Untitled movie";
        var suffix = $" ({movie.ReleaseYear}) | FilmHub #{movie.Id}";
        var title = Shorten(name, Math.Max(1, 70 - suffix.Length)) + suffix;
        var prefix = $"{Shorten(name, 32)} ({movie.ReleaseYear}), FilmHub #{movie.Id}. ";
        var summary = Plain(movie.Description);
        if (summary.Length == 0)
            summary = "Explore the movie overview, genres and community reviews on FilmHub.";
        var description = prefix + Shorten(summary, Math.Max(1, 160 - prefix.Length));
        var image = SeoUrls.Absolute(configuration, "seo/filmhub-social.png");
        var poster = movie.PosterUrl?.Trim();
        if (!string.IsNullOrEmpty(poster))
        {
            if (Uri.TryCreate(poster, UriKind.Absolute, out var external)
                && (external.Scheme == "http" || external.Scheme == "https")
                && string.IsNullOrEmpty(external.UserInfo))
                image = external.AbsoluteUri;
            else if (poster.StartsWith('/') && !poster.StartsWith("//") && !poster.Contains('\\'))
                image = SeoUrls.Absolute(configuration, poster);
        }
        return new MovieSeo(title, description, SeoUrls.Movie(configuration, movie.Id), image, $"Poster of {name}");
    }

    private static string Plain(string? value) => Regex.Replace(
        WebUtility.HtmlDecode(Regex.Replace(value ?? string.Empty, "<[^>]*>", " ")), @"\s+", " ").Trim();

    private static string Shorten(string value, int length)
    {
        if (value.Length <= length) return value;
        // Keep complete Unicode text elements, including surrogate pairs.
        var starts = StringInfo.ParseCombiningCharacters(value);
        var end = starts.LastOrDefault(index => index < length);
        return value[..end].TrimEnd() + "…";
    }
}
