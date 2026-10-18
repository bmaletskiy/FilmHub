
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FilmHub.Data;
using FilmHub.Models;

public class MoviesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public MoviesController(
        AppDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET: Movies
    public async Task<IActionResult> Index()
    {
        var movies = await _context.Movies
            .Include(m => m.Genres)
            .ToListAsync();

        return View(movies);
    }

    // GET: Movies/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var movie = await _context.Movies
            .Include(m => m.Genres)
            .Include(m => m.Reviews)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return NotFound();

        return View(movie);
    }

    // GET: Movies/Create
    public async Task<IActionResult> Create()
    {
        await LoadGenres();
        return View();
    }

    // POST: Movies/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Title,Description,ReleaseYear")] Movie movie,
        List<int> selectedGenreIds,
        IFormFile? posterFile)
    {
        var genres = await _context.Genres
            .Where(g => selectedGenreIds.Contains(g.Id))
            .ToListAsync();

        if (genres.Count == 0)
            ModelState.AddModelError(
                "selectedGenreIds",
                "Виберіть хоча б один жанр.");

        ValidatePoster(posterFile);

        if (ModelState.IsValid)
        {
            movie.Genres = genres;

            movie.PosterUrl = await SavePoster(posterFile);

            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        await LoadGenres(selectedGenreIds);
        return View(movie);
    }

    // GET: Movies/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var movie = await _context.Movies
            .Include(m => m.Genres)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return NotFound();

        await LoadGenres(movie.Genres.Select(g => g.Id));

        return View(movie);
    }

    // POST: Movies/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Id,Title,Description,ReleaseYear")] Movie input,
        List<int> selectedGenreIds,
        IFormFile? posterFile)
    {
        if (id != input.Id)
            return NotFound();

        var movie = await _context.Movies
            .Include(m => m.Genres)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return NotFound();

        var genres = await _context.Genres
            .Where(g => selectedGenreIds.Contains(g.Id))
            .ToListAsync();

        if (genres.Count == 0)
            ModelState.AddModelError(
                "selectedGenreIds",
                "Виберіть хоча б один жанр.");

        ValidatePoster(posterFile);

        if (!ModelState.IsValid)
        {
            await LoadGenres(selectedGenreIds);
            return View(input);
        }

        movie.Title = input.Title;
        movie.Description = input.Description;
        movie.ReleaseYear = input.ReleaseYear;

        movie.Genres.Clear();

        foreach (var genre in genres)
            movie.Genres.Add(genre);

        if (posterFile != null && posterFile.Length > 0)
            movie.PosterUrl = await SavePoster(posterFile);

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET: Movies/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var movie = await _context.Movies
            .Include(m => m.Genres)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return NotFound();

        return View(movie);
    }

    // POST: Movies/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var movie = await _context.Movies.FindAsync(id);

        if (movie != null)
        {
            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadGenres(IEnumerable<int>? selectedIds = null)
    {
        var genres = await _context.Genres
            .OrderBy(g => g.Name)
            .ToListAsync();

        ViewBag.AllGenres = genres;
        ViewBag.SelectedGenreIds = selectedIds?.ToHashSet() ?? new HashSet<int>();
    }

    private void ValidatePoster(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return;

        string extension = Path.GetExtension(file.FileName)
            .ToLowerInvariant();

        string[] allowed = [".jpg", ".jpeg", ".png", ".webp"];

        if (!allowed.Contains(extension) ||
            !file.ContentType.StartsWith("image/"))
        {
            ModelState.AddModelError(
                "posterFile",
                "Підтримуються тільки JPG, PNG та WEBP.");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError(
                "posterFile",
                "Максимальний розмір постера — 5 МБ.");
        }
    }

    private async Task<string?> SavePoster(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        var uploadsPath = Path.Combine(
            _environment.WebRootPath,
            "images");

        Directory.CreateDirectory(uploadsPath);

        var fileName = Guid.NewGuid().ToString("N") +
            Path.GetExtension(file.FileName).ToLowerInvariant();

        var filePath = Path.Combine(uploadsPath, fileName);

        await using var stream = new FileStream(
            filePath,
            FileMode.CreateNew);

        await file.CopyToAsync(stream);

        return "/images/" + fileName;
    }
}
