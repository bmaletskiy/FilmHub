using System.ComponentModel.DataAnnotations;

namespace FilmHub.Contracts.Movies;

public sealed class MoviePageQuery
{
    public const int DefaultLimit = 20;
    public const int MaximumLimit = 100;

    [Range(0, int.MaxValue)]
    public int? Skip { get; init; }

    [Range(1, MaximumLimit)]
    public int? Limit { get; init; }
}
