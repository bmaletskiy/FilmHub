# B1 — Pagination API (skip, limit, nextLink)

## Contract and compatibility

`GET /api/movies` without `skip` and `limit` retains the original full JSON array for existing C5 clients. Adding **either** parameter opts into an envelope. `page`/`pageSize` are not pagination aliases and still follow the legacy behavior.

```http
GET /api/movies?skip=0&limit=2
```

Illustrative response shape (IDs and totals depend on the current database):

```json
{
  "items": [
    { "id": 1, "title": "Example", "description": "Overview", "releaseYear": 2026,
      "posterUrl": null, "genres": [{ "id": 1, "name": "Drama" }] }
  ],
  "totalCount": 5,
  "skip": 0,
  "limit": 2,
  "nextLink": "/api/movies?skip=2&limit=2"
}
```

The shape example abbreviates the `items` array; real responses return up to `limit` complete movie DTOs. `totalCount` counts movies, including those not on the current page. `skip` counts rows to skip, not ID values; gaps in IDs do not affect paging. `limit` is the maximum number of movies, not the number of joined genre rows.

| Parameter | Default when pagination is enabled | Accepted |
| --- | --- | --- |
| skip | 0 | Integer 0–2147483647 |
| limit | 20 | Integer 1–100 |

Negative, malformed, overflowing, empty or repeated parameters return 400 `application/problem+json` with validation errors. Oversized limits are rejected, not silently clamped. An offset at/beyond the end returns 200 with empty `items`, the real `totalCount` and `nextLink: null`. Empty databases return the same envelope with total 0.

`nextLink` is null on the last page; otherwise it advances `skip` by `limit`, keeps the effective limit, preserves other query values and URL-encodes them. It is a root-relative URL including `PathBase`, so clients resolve it against the same origin (e.g. `http://localhost:8081` in Docker). No hardcoded public host or untrusted forwarded headers are required.

## Implementation and B9

EF executes `COUNT` and `OrderBy(Id).Skip(skip).Take(limit)` in the database before materializing DTOs. Genres are included using existing relationships. The primary-key ordering is stable and unique. Count and page are separate queries; concurrent writes between them or between page requests can change totals/boundaries. This is ordinary offset pagination, not a cross-request snapshot. No schema changes, migrations or new dependencies are required.

Existing B9 keys already vary by all query parameters and the data version: distinct skip/limit combinations and the legacy array never share entries. Successful POST/PUT/DELETE and other tracked Movie/Genre/link saves invalidate every movie page, its total and nextLink, plus legacy/detail caches. Failed validation does not publish a cache entry. A cold paginated GET executes two reader commands (count and page); a warm hit executes none. B9 TTL, memory bounds and in-flight version protection are unchanged.

`GET /api/movies/{id}` and all C5 write contracts remain unchanged. MVC, F1/F2, C7, Analytics, secrets and CI/CD files are not changed by B1.

## Docker demonstration (read-only API requests)

From PowerShell:

```powershell
Set-Location D:\.NET\projects\FilmHub
docker compose ps
# Existing db service must already be running.
docker compose up -d --build --no-deps app
docker compose logs --tail 30 app
```

This rebuilds/recreates only `app` using the existing compose configuration, leaving the running database service and existing named volumes in place. No migration, seed or volume deletion command is needed. The compose file publishes `8081:8080`.

1. Open `FilmHub.Api.http`; retain `baseUrl = http://localhost:8081`. Find the **B1** section. Set a new `b1Run` for a cold key.
2. Execute **B1 1** (`skip=0&limit=2`). Show `items` ordered by ID, `totalCount`, `skip`, `limit` and `nextLink`.
3. Execute **B1 2** within 60 seconds. Same JSON, response `Age` header (including 0) proves a B9 hit. Change offset/limit using **B1 4–5** to see independent entries.
4. Copy the exact `nextLink` value into `b1NextLink`, then run **B1 3**. It is relative: the HTTP file prepends `baseUrl`. Continue following links to the end. If the database has fewer than three movies, use limit 1; a database with zero/one movie legitimately has no second page.
5. Set `b1LastSkip` to `max(0, totalCount - 1)` and execute **B1 6**; `nextLink` must be null. **B1 7** always demonstrates an empty out-of-range page.
6. Execute **B1 10–13**: each returns 400. **B1 14** shows that old clients still receive an array.

All B1 HTTP examples are GET-only. CRUD invalidation is demonstrated automatically with SQLite in memory, so no real film needs to be inserted/updated/deleted for this video:

```powershell
dotnet test --no-restore --filter FullyQualifiedName~MoviesPaginationTests
```

The suite warms several pages, performs isolated POST/PUT/DELETE, and verifies fresh items, totals, boundaries, nextLink, legacy arrays and details. It also covers defaults, maximum limit, invalid/duplicate parameters, empty/out-of-range pages, actual SQL LIMIT/OFFSET, complete genres, and following every nextLink without duplicates on unchanged data.

Docker CLI was unavailable in this execution environment; Docker runtime checks are not claimed. Local builds and integration tests are the verification used here. No Docker commands that change containers or volumes were executed.

## Local verification result

On 2026-10-26, `dotnet test --no-restore` passed **114 tests** (79 previous cases plus 35 B1 cases), with 0 failed or skipped. `dotnet build --no-restore` completed with 0 errors; existing NU1901 warnings for NuGet.Packaging and NuGet.Protocol remain. Logs and TRX results are under the local ignored `obj/B1Verification/` directory.

SHA256 comparison confirmed 142 pre-existing files outside the intended API/test/example/documentation edits were unchanged, including `.env`, development settings, MVC, F1/F2/C7, Analytics, B9 implementation, Docker configuration and CI/CD. No real PostgreSQL connections, secret edits, commits or pushes were performed.

EF design reference: [Microsoft EF Core pagination](https://learn.microsoft.com/en-us/ef/core/querying/pagination).
