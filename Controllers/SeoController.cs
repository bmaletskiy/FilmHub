using System.Text;
using System.Xml.Linq;
using FilmHub.Data;
using FilmHub.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FilmHub.Controllers;

public class SeoController : Controller
{
    private const int PageSize = 1000;
    private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public SeoController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("/robots.txt")]
    public IActionResult Robots() => Content(
        "User-agent: *\nAllow: /\n\nSitemap: " + SeoUrls.Absolute(_configuration, "sitemap.xml") + "\n",
        "text/plain", Encoding.UTF8);

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken)
    {
        var count = await _context.Movies.AsNoTracking().CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        var xml = new XElement(SitemapNamespace + "sitemapindex",
            Enumerable.Range(1, pages).Select(page => new XElement(SitemapNamespace + "sitemap",
                new XElement(SitemapNamespace + "loc", SeoUrls.Absolute(_configuration, $"sitemaps/movies-{page}.xml")))));
        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("/sitemaps/movies-{page:int}.xml")]
    public async Task<IActionResult> MoviesSitemap(int page, CancellationToken cancellationToken)
    {
        if (page < 1 || page > int.MaxValue / PageSize) return NotFound();
        var ids = await _context.Movies.AsNoTracking().OrderBy(movie => movie.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(movie => movie.Id).ToListAsync(cancellationToken);
        if (ids.Count == 0 && page > 1) return NotFound();
        var urls = ids.Select(id => SeoUrls.Movie(_configuration, id)).ToList();
        if (page == 1)
        {
            urls.Insert(0, SeoUrls.Absolute(_configuration, ""));
            urls.Insert(1, SeoUrls.Absolute(_configuration, "Movies"));
        }
        var xml = new XElement(SitemapNamespace + "urlset",
            urls.Select(url => new XElement(SitemapNamespace + "url", new XElement(SitemapNamespace + "loc", url))));
        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }
}
