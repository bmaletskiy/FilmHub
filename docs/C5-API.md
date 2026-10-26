# C5: Movies REST API

The API uses the existing `Movie`, `Genre`, `Review` and `AppDbContext`. MVC CRUD and its file-upload flow remain unchanged. No schema changes or migrations are required.

| Request | Success | Errors |
| --- | --- | --- |
| GET /api/movies | 200, array ordered by ID (empty array if no movies) | |
| GET /api/movies/{id} | 200, movie DTO | 404 |
| POST /api/movies | 201, movie DTO and followable Location | 400, 415 |
| PUT /api/movies/{id} | 204, full replacement | 400, 404, 409, 415 |
| DELETE /api/movies/{id} | 204 | 404, 409 |

Write DTO: `title` (required, nonblank, max 200), `description` (required, nonblank, max 10000), `releaseYear` (1888–2100), optional `posterUrl` (max 2048; HTTP(S) without credentials or a site-relative path), `genreIds` (required, 1–100 positive IDs). Strings are trimmed. Null/blank poster clears the URL on PUT. File upload remains in MVC.

At least one existing genre is required, matching MVC behavior. Duplicate IDs are deduplicated; any unknown genre rejects the entire write with 400 and an error under `GenreIds`. PUT replaces the complete genre selection. Genres themselves are never created or deleted through this API. Responses include `genres: [{ id, name }]`; navigation properties and reviews are not serialized. Fields such as `id`, nested `genres` and `reviews` are not bindable through the request DTO.

Validation errors use `application/problem+json` with an `errors` object. Missing movies return 404. DELETE cascades reviews and join-table rows using the existing EF relationships, preserves genre rows and does not remove poster files. Concurrent deletion detected during PUT/DELETE returns 404; other EF concurrency conflicts return 409. The existing application has no authentication; this API follows that setup.

## Run and demonstrate

```powershell
dotnet build FilmHub.slnx
dotnet test tests/FilmHub.Api.Tests/FilmHub.Api.Tests.csproj
dotnet run --project FilmHub.csproj --launch-profile http
```

Open `FilmHub.Api.http` in Visual Studio or an HTTP-file client. The default URL matches the existing HTTP launch profile: `http://localhost:5103`. Set `genreId` to an existing genre from `/Genres`, run POST, then GET/PUT/DELETE for the returned ID. Named response syntax is supported by Visual Studio and REST Client; if unavailable, set `movieId` manually to the ID from POST. Only run mutation examples against a database intended for demonstration. Running the app itself does not seed or migrate data.

## Automated checks and isolation

Tests boot the actual `Program` via `WebApplicationFactory`, replace both the PostgreSQL context options and their configuration callbacks with SQLite, and assert that the active provider is SQLite before creating test tables. Each test has a private `:memory:` connection with foreign keys enabled. No real PostgreSQL connections, Docker, secrets edits, file uploads or migrations are used.

Tests cover HTTP CRUD, JSON DTO shape, Location, persistence, full genre replacement, duplicate and missing genre IDs, validation, malformed bodies, overposting, unsupported content types, delete cascades, and MVC/F1/F2/C7/Analytics regression paths. SQLite checks relational behavior; PostgreSQL-specific execution is outside this isolated suite.

Verified locally on 2026-10-26: all 64 tests passed (0 failed, 0 skipped); `dotnet build --no-restore` built the solution with 0 errors. Existing NU1901 warnings for NuGet.Packaging and NuGet.Protocol 6.12.1 remain. SHA256 comparison confirmed 131 pre-existing files outside the three intended project/entry-point edits were unchanged, including MVC, F1/F2/C7, Analytics, `.env` and development settings. No commit, push, deployment, Docker commands or real database mutations were performed.

Implementation conventions follow [ASP.NET Core web APIs](https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0) and [integration testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0).
