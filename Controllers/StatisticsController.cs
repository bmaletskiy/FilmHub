using FilmHub.Data;
using FilmHub.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FilmHub.Controllers;

public class StatisticsController : Controller
{
    private readonly AppDbContext _context;

    public StatisticsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new StatisticsViewModel
        {
            ByReleaseYear = await _context.Movies.AsNoTracking()
                .GroupBy(movie => movie.ReleaseYear)
                .OrderBy(group => group.Key)
                .Select(group => new ReleaseYearStatistic
                {
                    Year = group.Key,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken),
            ByGenre = await _context.Genres.AsNoTracking()
                .Select(genre => new GenreStatistic
                {
                    Name = genre.Name,
                    Count = genre.Movies.Count()
                })
                .OrderByDescending(genre => genre.Count)
                .ThenBy(genre => genre.Name)
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }
}
