using FilmHub.Contracts.Movies;
using FilmHub.Caching;
using FilmHub.Data;
using FilmHub.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;

namespace FilmHub.Controllers.Api;

[ApiController]
[Route("api/movies")]
public sealed class MoviesApiController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    [OutputCache(PolicyName = MovieApiCache.Policy)]
    // Legacy: MovieResponse[]. With skip/limit: MoviePageResponse (see docs/B1-PAGINATION.md).
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll([FromQuery(Name = "")] MoviePageQuery page, CancellationToken cancellationToken)
    {
        foreach (var name in new[] { "skip", "limit" })
        {
            if (Request.Query.TryGetValue(name, out var values)
                && (values.Count != 1 || string.IsNullOrWhiteSpace(values[0])))
                ModelState.AddModelError(name, "Specify one non-empty integer value.");
        }
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var query = context.Movies.AsNoTracking();
        if (page.Skip is null && page.Limit is null)
        {
            // Keep old clients and the original C5 JSON-array contract working.
            var all = await query.Include(m => m.Genres).OrderBy(m => m.Id).ToListAsync(cancellationToken);
            return Ok(all.Select(MovieResponse.FromMovie).ToArray());
        }

        var skip = page.Skip ?? 0;
        var limit = page.Limit ?? MoviePageQuery.DefaultLimit;
        var totalCount = await query.CountAsync(cancellationToken);
        var movies = await query.Include(m => m.Genres).OrderBy(m => m.Id)
            .Skip(skip).Take(limit).ToListAsync(cancellationToken);
        // Use long arithmetic so a valid, large skip cannot overflow when adding limit.
        var nextSkip = (long)skip + limit;
        string? nextLink = null;
        if (movies.Count > 0 && nextSkip < totalCount)
        {
            var parameters = Request.Query.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            parameters.Remove("skip");
            parameters.Remove("limit");
            parameters["skip"] = nextSkip.ToString(CultureInfo.InvariantCulture);
            parameters["limit"] = limit.ToString(CultureInfo.InvariantCulture);
            // Preserve other query values and PathBase, without trusting Host/proxy headers.
            nextLink = QueryHelpers.AddQueryString(Request.PathBase.Add(Request.Path).Value!, parameters);
        }
        return Ok(new MoviePageResponse(movies.Select(MovieResponse.FromMovie).ToArray(), totalCount, skip, limit, nextLink));
    }

    [HttpGet("{id:int}")]
    [OutputCache(PolicyName = MovieApiCache.Policy)]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovieResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var movie = await context.Movies.AsNoTracking().Include(m => m.Genres)
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        return movie is null ? NotFound() : Ok(MovieResponse.FromMovie(movie));
    }

    [HttpPost]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MovieResponse>> Create(MovieRequest request, CancellationToken cancellationToken)
    {
        var genres = await LoadGenres(request.GenreIds, cancellationToken);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var movie = new Movie();
        Apply(movie, request, genres);
        context.Movies.Add(movie);
        await context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = movie.Id }, MovieResponse.FromMovie(movie));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, MovieRequest request, CancellationToken cancellationToken)
    {
        var movie = await context.Movies.Include(m => m.Genres)
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (movie is null) return NotFound();

        var genres = await LoadGenres(request.GenreIds, cancellationToken);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        Apply(movie, request, genres);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            if (!await context.Movies.AnyAsync(m => m.Id == id, cancellationToken)) return NotFound();
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "The movie changed during the update. Retry the request.");
        }
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var movie = await context.Movies.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (movie is null) return NotFound();
        // Existing EF relationships cascade reviews and join rows, preserving the genres.
        context.Movies.Remove(movie);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            if (!await context.Movies.AnyAsync(m => m.Id == id, cancellationToken)) return NotFound();
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "The movie changed during deletion. Retry the request.");
        }
        return NoContent();
    }

    private async Task<List<Genre>> LoadGenres(int[] genreIds, CancellationToken cancellationToken)
    {
        var ids = genreIds.Distinct().ToArray();
        var genres = await context.Genres.Where(g => ids.Contains(g.Id)).ToListAsync(cancellationToken);
        var missing = ids.Except(genres.Select(g => g.Id)).Order().ToArray();
        if (missing.Length > 0)
            ModelState.AddModelError(nameof(MovieRequest.GenreIds), $"Unknown genre IDs: {string.Join(", ", missing)}.");
        return genres;
    }

    private static void Apply(Movie movie, MovieRequest request, List<Genre> genres)
    {
        movie.Title = request.Title.Trim();
        movie.Description = request.Description.Trim();
        movie.ReleaseYear = request.ReleaseYear;
        movie.PosterUrl = string.IsNullOrWhiteSpace(request.PosterUrl) ? null : request.PosterUrl.Trim();
        movie.Genres.Clear();
        movie.Genres.AddRange(genres);
    }
}
