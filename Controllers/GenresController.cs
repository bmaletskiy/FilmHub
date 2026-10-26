
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FilmHub.Models;
using FilmHub.Data;

public class GenresController : Controller
{
    private readonly AppDbContext _context;

    public GenresController(AppDbContext context)
    {
        _context = context;
    }

    // Select2 AJAX: do not query the database before three characters.
    [HttpGet]
    public async Task<IActionResult> Search(string? term, CancellationToken cancellationToken)
    {
        term = term?.Trim();
        if (string.IsNullOrEmpty(term) || term.Length < 3)
            return Json(new { results = Array.Empty<object>() });

        // Escape LIKE wildcards so the input is treated as a literal substring.
        var escaped = term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var results = await _context.Genres.AsNoTracking()
            .Where(g => EF.Functions.ILike(g.Name, "%" + escaped + "%", "\\"))
            .OrderBy(g => g.Name).ThenBy(g => g.Id)
            .Take(30)
            .Select(g => new { id = g.Id, text = g.Name })
            .ToListAsync(cancellationToken);

        return Json(new { results });
    }
    // GET: GENRES
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Genres.ToListAsync());
    }

    // GET: GENRES/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var genre = await _context.Genres
            .FirstOrDefaultAsync(m => m.Id == id);
        if (genre == null)
        {
            return NotFound();
        }

        return View(genre);
    }

    // GET: GENRES/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: GENRES/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name,Movies")] Genre genre)
    {
        if (ModelState.IsValid)
        {
            _context.Add(genre);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(genre);
    }

    // GET: GENRES/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var genre = await _context.Genres.FindAsync(id);
        if (genre == null)
        {
            return NotFound();
        }
        return View(genre);
    }

    // POST: GENRES/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Name,Movies")] Genre genre)
    {
        if (id != genre.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(genre);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!GenreExists(genre.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(genre);
    }

    // GET: GENRES/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var genre = await _context.Genres
            .FirstOrDefaultAsync(m => m.Id == id);
        if (genre == null)
        {
            return NotFound();
        }

        return View(genre);
    }

    // POST: GENRES/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var genre = await _context.Genres.FindAsync(id);
        if (genre != null)
        {
            _context.Genres.Remove(genre);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool GenreExists(int? id)
    {
        return _context.Genres.Any(e => e.Id == id);
    }
}
