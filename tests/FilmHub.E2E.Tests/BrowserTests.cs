using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FilmHub.E2E.Tests;

public sealed class BrowserTests : IAsyncLifetime
{
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private IBrowserContext context = null!;
    private IPage page = null!;
    private readonly string marker = "FilmHub-QA2:" + Guid.NewGuid();
    private readonly string title = "FilmHub QA2 " + Guid.NewGuid();
    private string? artifactDirectory;
    private Uri origin = null!;

    public async Task InitializeAsync()
    {
        origin = LocalOrigin(Environment.GetEnvironmentVariable("FILMHUB_E2E_BASE_URL") ?? "http://localhost:8081");
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = Environment.GetEnvironmentVariable("FILMHUB_E2E_HEADED") != "1",
            SlowMo = Environment.GetEnvironmentVariable("FILMHUB_E2E_HEADED") == "1" ? 250 : 0
        });
        artifactDirectory = Path.Combine(AppContext.BaseDirectory, "TestResults", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);
        context = await browser.NewContextAsync(new()
        {
            BaseURL = origin.AbsoluteUri, ViewportSize = new() { Width = 1440, Height = 1000 },
            RecordVideoDir = artifactDirectory
        });
        context.SetDefaultTimeout(15000);
        // Block external document navigation and all external writes, including redirects.
        // CDN scripts and read-only images remain available to the real application.
        await context.RouteAsync("**/*", async route =>
        {
            var uri = new Uri(route.Request.Url);
            bool sameOrigin = uri.GetLeftPart(UriPartial.Authority) == origin.GetLeftPart(UriPartial.Authority);
            if (!sameOrigin && (route.Request.IsNavigationRequest || route.Request.Method is not ("GET" or "HEAD")))
                await route.AbortAsync();
            else await route.ContinueAsync();
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        page = await context.NewPageAsync();
    }

    private static Uri LocalOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !(uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "[::1]")
            || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("E2E requires a loopback HTTP(S) origin, for example http://localhost:8081. Remote servers are forbidden.");
        return uri;
    }

    [Fact]
    public async Task Home_opens()
    {
        await Open("/");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "FilmHub", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Browse Movies", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Movies_catalog_opens()
    {
        await Open("/Movies");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Movies", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Add Movie", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Locator("table thead")).ToContainTextAsync("Title");
    }

    [Fact]
    public async Task Statistics_opens()
    {
        await Open("/Statistics");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Movie statistics", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Locator("summary")).ToHaveTextAsync("View data tables");
        await page.Locator("summary").ClickAsync();
        await Expect(page.Locator("table")).ToHaveCountAsync(2);
        await Expect(page.Locator("table").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_connects_Home_Movies_and_Statistics()
    {
        await Open("/");
        var navigation = page.GetByRole(AriaRole.Navigation);
        await navigation.GetByRole(AriaRole.Link, new() { Name = "Movies", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex(@"/Movies/?$"));
        await Expect(page.Locator("h1")).ToHaveTextAsync("Movies");
        await navigation.GetByRole(AriaRole.Link, new() { Name = "Statistics", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex(@"/Statistics/?$"));
        await Expect(page.Locator("h1")).ToHaveTextAsync("Movie statistics");
        await navigation.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "FilmHub", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Own_movie_is_created_edited_and_deleted_in_browser()
    {
        // Read a real genre name from MVC; never create or modify genres.
        await Open("/Genres");
        var genreName = (await page.Locator("table tbody tr td").First.InnerTextAsync()).Trim();
        Assert.True(genreName.Length >= 3, "A local genre with a name of at least three characters is required for Select2 search.");
        await Open("/Movies");
        await page.GetByRole(AriaRole.Link, new() { Name = "Add Movie", Exact = true }).ClickAsync();
        await page.GetByLabel("Title", new() { Exact = true }).FillAsync(title);
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync(marker);
        await page.Locator("#ReleaseYear").FillAsync("2026");
        await page.Locator(".select2-selection").ClickAsync();
        await page.Locator(".select2-search__field").FillAsync(genreName);
        await page.Locator(".select2-results__option[aria-selected]").Filter(new() { HasTextRegex = new Regex("^" + Regex.Escape(genreName) + "$") }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex(@"/Movies/?$"));
        var row = OwnedRow(title);
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync(marker);
        await Expect(row).ToContainTextAsync("2026");
        await Expect(row).ToContainTextAsync(genreName);
        await row.GetByRole(AriaRole.Link, new() { Name = "Edit", Exact = true }).ClickAsync();
        await Expect(page.GetByLabel("Title", new() { Exact = true })).ToHaveValueAsync(title);
        await Expect(page.GetByLabel("Description", new() { Exact = true })).ToHaveValueAsync(marker);
        await page.GetByLabel("Title", new() { Exact = true }).FillAsync(title + " updated");
        await page.Locator("#ReleaseYear").FillAsync("2025");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save Changes", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex(@"/Movies/?$"));
        row = OwnedRow(title + " updated");
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync("2025");
        await row.GetByRole(AriaRole.Link, new() { Name = "Details", Exact = true }).ClickAsync();
        await Expect(page.Locator("main")).ToContainTextAsync(title + " updated");
        await Expect(page.Locator("main")).ToContainTextAsync(marker);
        var detailsUrl = page.Url;
        await Open("/Movies");
        await OwnedRow(title + " updated").GetByRole(AriaRole.Link, new() { Name = "Delete", Exact = true }).ClickAsync();
        await Expect(page.Locator("main")).ToContainTextAsync(marker);
        await Expect(page.Locator("h3")).ToHaveTextAsync(title + " updated");
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirm Delete", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex(@"/Movies/?$"));
        await Expect(OwnedRow(title + " updated")).ToHaveCountAsync(0);
        var missing = await context.APIRequest.GetAsync(
            detailsUrl,
            new() { MaxRedirects = 0 }
        );
        Assert.Equal(404, missing.Status);
    }

    private ILocator OwnedRow(string name) => page.Locator("table tbody tr")
        .Filter(new() { Has = page.GetByRole(AriaRole.Cell, new() { Name = name, Exact = true }) })
        .Filter(new() { HasText = marker });

    private async Task Open(string path)
    {
        var response = await page.GotoAsync(path);
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Equal(origin.GetLeftPart(UriPartial.Authority), new Uri(page.Url).GetLeftPart(UriPartial.Authority));
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (context is not null)
            {
                try
                {
                    if (page is not null) await page.ScreenshotAsync(new() { Path = Path.Combine(artifactDirectory!, "last-page.png"), FullPage = true });
                }
                finally
                {
                    // Use a separate context: cleanup still works after a failed/closed page.
                    // Read-only list + exact per-test ownership check, then API deletion.
                    var cleanup = await browser.NewContextAsync(new() { BaseURL = origin.AbsoluteUri });
                    try
                    {
                        var response = await cleanup.APIRequest.GetAsync("/api/movies", new() { MaxRedirects = 0 });
                        Assert.Equal(200, response.Status);
                        using var json = JsonDocument.Parse(await response.TextAsync());
                        foreach (var movie in json.RootElement.EnumerateArray())
                        {
                            var name = movie.GetProperty("title").GetString();
                            if (movie.GetProperty("description").GetString() != marker || (name != title && name != title + " updated")) continue;
                            var path = "/api/movies/" + movie.GetProperty("id").GetInt32();
                            var detail = await cleanup.APIRequest.GetAsync(path, new() { MaxRedirects = 0 });
                            Assert.Equal(200, detail.Status);
                            using var current = JsonDocument.Parse(await detail.TextAsync());
                            Assert.Equal(marker, current.RootElement.GetProperty("description").GetString());
                            Assert.Equal(name, current.RootElement.GetProperty("title").GetString());
                            var deleted = await cleanup.APIRequest.DeleteAsync(path, new() { MaxRedirects = 0 });
                            Assert.Equal(204, deleted.Status);
                        }
                    }
                    finally { await cleanup.CloseAsync(); }
                }
            }
        }
        finally
        {
            try
            {
                if (context is not null)
                {
                    try { await context.Tracing.StopAsync(new() { Path = Path.Combine(artifactDirectory!, "trace.zip") }); }
                    finally { await context.CloseAsync(); }
                }
            }
            finally
            {
                if (browser is not null) await browser.CloseAsync();
                playwright?.Dispose();
            }
        }
    }
}
