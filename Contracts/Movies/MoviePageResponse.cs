namespace FilmHub.Contracts.Movies;

public sealed record MoviePageResponse(
    IReadOnlyList<MovieResponse> Items, int TotalCount, int Skip, int Limit, string? NextLink);
