using FilmHub.Contracts.Movies;
using FilmHub.Data;
using FilmHub.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FilmHub.Controllers.Api;

[ApiController]
[Route("api/movies")]
public sealed class MoviesApiController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MovieResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovieResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var movies = await context.Movies.AsNoTracking().Include(m => m.Genres)
            .OrderBy(m => m.Id).ToListAsync(cancellationToken);
        return Ok(movies.Select(MovieResponse.FromMovie).ToArray());
    }

    [HttpGet("{id:int}")]
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
