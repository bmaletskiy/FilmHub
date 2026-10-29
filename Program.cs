using FilmHub.Services;
using Microsoft.EntityFrameworkCore;
using FilmHub.Data;
using FilmHub.Caching;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<TelegramNotificationService>();
builder.Services.AddSingleton<CloudinaryStorageService>();

// HTTP response compression
builder.Services.AddResponseCompression();

builder.Services.AddSingleton<MovieApiCache>();
builder.Services.AddScoped<MovieCacheInvalidationInterceptor>();
builder.Services.AddOutputCache(options =>
{
    options.SizeLimit = 32 * 1024 * 1024;
    options.MaximumBodySize = 1024 * 1024;
    options.AddPolicy(MovieApiCache.Policy, policy => policy
        .Expire(TimeSpan.FromSeconds(60))
        .SetVaryByQuery("*")
        .Tag(MovieApiCache.Tag)
        .VaryByValue(http => new KeyValuePair<string, string>("movies-version",
            http.RequestServices.GetRequiredService<MovieApiCache>().Version)));
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseNpgsql(connectionString)
        .AddInterceptors(services.GetRequiredService<MovieCacheInvalidationInterceptor>()));

var app = builder.Build();

// Enable HTTP response compression
app.UseResponseCompression();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseOutputCache();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Allows the integration tests to boot the real application with an isolated DbContext.
public partial class Program { }
