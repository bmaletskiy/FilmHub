using FilmHub.Data;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;

namespace FilmHub.Services;

public class MovieSearchService : IDisposable
{
    private const LuceneVersion LV = LuceneVersion.LUCENE_48;
    private static readonly string[] Fields = ["title", "genres", "description"];

    private readonly IServiceScopeFactory _scopes;
    private readonly StandardAnalyzer _analyzer = new(LV);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private RAMDirectory _directory = new();
    private volatile bool _dirty = true;

    public MovieSearchService(IServiceScopeFactory scopes) => _scopes = scopes;

    public void MarkDirty() => _dirty = true;

    // Повертає id фільмів, найрелевантніші першими
    public async Task<List<int>> SearchAsync(string query, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        await EnsureIndexAsync();

        using var reader = DirectoryReader.Open(_directory);
        var searcher = new IndexSearcher(reader);

        var boosts = new Dictionary<string, float>
        {
            ["title"] = 3f,
            ["genres"] = 2f,
            ["description"] = 1f
        };
        var parser = new MultiFieldQueryParser(LV, Fields, _analyzer, boosts);

        Query parsed;
        try
        {
            parsed = parser.Parse(QueryParserBase.Escape(query.Trim()));
        }
        catch (ParseException)
        {
            return [];
        }

        return searcher.Search(parsed, limit).ScoreDocs
            .Select(hit => int.Parse(searcher.Doc(hit.Doc).Get("id")))
            .ToList();
    }

    private async Task EnsureIndexAsync()
    {
        if (!_dirty) return;

        await _lock.WaitAsync();
        try
        {
            if (!_dirty) return;
            _dirty = false;

            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var movies = await db.Movies
                    .AsNoTracking()
                    .Include(m => m.Genres)
                    .ToListAsync();

                var fresh = new RAMDirectory();
                using (var writer = new IndexWriter(fresh, new IndexWriterConfig(LV, _analyzer)))
                {
                    foreach (var m in movies)
                    {
                        writer.AddDocument(new Document
                        {
                            new StringField("id", m.Id.ToString(), Field.Store.YES),
                            new TextField("title", m.Title, Field.Store.NO),
                            new TextField("genres", string.Join(" ", m.Genres.Select(g => g.Name)), Field.Store.NO),
                            new TextField("description", m.Description ?? "", Field.Store.NO)
                        });
                    }
                    writer.Commit();
                }

                _directory = fresh;
            }
            catch
            {
                _dirty = true;
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _analyzer.Dispose();
        _lock.Dispose();
    }
}