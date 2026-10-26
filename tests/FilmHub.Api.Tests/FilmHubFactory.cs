using FilmHub.Data;
using FilmHub.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FilmHub.Api.Tests;

// Each test owns an independent, ephemeral relational database. Never uses PostgreSQL.
public sealed class FilmHubFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            connection.Open();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        });
    }

    public HttpClient CreateSeededClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.Database.IsSqlite()) throw new InvalidOperationException("Tests require SQLite.");
        db.Database.EnsureCreated();
        var drama = new Genre { Id = 1, Name = "Drama" };
        var comedy = new Genre { Id = 2, Name = "Comedy" };
        db.Genres.AddRange(drama, comedy);
        db.Movies.Add(new Movie
        {
            Id = 1, Title = "Original", Description = "Original overview", ReleaseYear = 2020,
            PosterUrl = "/images/original.jpg", Genres = [drama],
            Reviews = [new Review { Author = "Tester", Text = "Review", Rating = 8 }]
        });
        db.SaveChanges();
        return client;
    }

    public async Task<T> ReadDatabase<T>(Func<AppDbContext, Task<T>> read)
    {
        using var scope = Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}
