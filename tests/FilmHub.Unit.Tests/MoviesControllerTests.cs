using FilmHub.Data;
using FilmHub.Models;
using FilmHub.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FilmHub.Unit.Tests;

public class MoviesControllerTests
{
    private readonly Mock<ICloudinaryStorageService> _cloudinary = new();
    private readonly Mock<ITelegramNotificationService> _telegram = new();

    // Нова порожня база в пам'яті для кожного тесту
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private MoviesController CreateController(AppDbContext db) =>
        new(db, Mock.Of<IWebHostEnvironment>(), _telegram.Object, _cloudinary.Object);

    [Fact]
    public async Task Details_NullId_ReturnsNotFound()
    {
        using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Details(null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_UnknownId_ReturnsNotFound()
    {
        using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Details(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ExistingId_ReturnsViewWithMovie()
    {
        using var db = CreateDb();
        db.Movies.Add(new Movie
        {
            Id = 1,
            Title = "Inception",
            Description = "Dream heist",
            ReleaseYear = 2010
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Details(1);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Movie>(view.Model);
        Assert.Equal("Inception", model.Title);
    }

    [Fact]
    public async Task Create_NoGenres_ReturnsFormAndCallsNoServices()
    {
        using var db = CreateDb();
        var controller = CreateController(db);
        var movie = new Movie { Title = "Inception", Description = "Dream heist", ReleaseYear = 2010 };

        var result = await controller.Create(movie, new List<int>(), null);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(db.Movies);
        _cloudinary.Verify(c => c.UploadPosterAsync(It.IsAny<IFormFile?>()), Times.Never);
        _telegram.Verify(t => t.SendNewMovieNotificationAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Create_ValidMovie_SavesAndNotifiesOnce()
    {
        using var db = CreateDb();
        db.Genres.Add(new Genre { Id = 1, Name = "Drama" });
        await db.SaveChangesAsync();
        _cloudinary.Setup(c => c.UploadPosterAsync(It.IsAny<IFormFile?>()))
            .ReturnsAsync("https://img.example/poster.jpg");
        var controller = CreateController(db);
        var movie = new Movie { Title = "Inception", Description = "Dream heist", ReleaseYear = 2010 };

        var result = await controller.Create(movie, new List<int> { 1 }, null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        var saved = Assert.Single(db.Movies);
        Assert.Equal("https://img.example/poster.jpg", saved.PosterUrl);
        _cloudinary.Verify(c => c.UploadPosterAsync(It.IsAny<IFormFile?>()), Times.Once);
        _telegram.Verify(t => t.SendNewMovieNotificationAsync("Inception", 2010, "Drama"), Times.Once);
    }

    [Fact]
    public async Task Create_TelegramFails_MovieIsStillSaved()
    {
        using var db = CreateDb();
        db.Genres.Add(new Genre { Id = 1, Name = "Drama" });
        await db.SaveChangesAsync();
        _telegram.Setup(t => t.SendNewMovieNotificationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Telegram is down"));
        var controller = CreateController(db);
        var movie = new Movie { Title = "Inception", Description = "Dream heist", ReleaseYear = 2010 };

        var result = await controller.Create(movie, new List<int> { 1 }, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(db.Movies);
    }
}