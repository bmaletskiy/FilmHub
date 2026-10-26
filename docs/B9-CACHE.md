# B9 — REST API response caching

`GET /api/movies` and `GET /api/movies/{id}` use the built-in ASP.NET Core `OutputCache`. Responses live for 60 seconds. The process-local store is limited to 32 MiB and individual response bodies to 1 MiB; oversized lists are served normally but not cached. Default request locking remains enabled. Only successful public responses are cached; errors, authenticated/Authorization-bearing requests and responses setting cookies follow the framework's default exclusions. MVC and SEO responses are not cached by this policy.

## Freshness and pagination

The key includes the scheme, host, path, **all query parameters**, and a movie-data version. B1 now supports `skip`/`limit` with a paged envelope; requests without these parameters retain the C5 array. Different offsets, limits and other query values have independent entries. The old `page`/`pageSize` examples still demonstrate query variation but do not enable pagination. All pages are invalidated on writes because inserting/deleting/updating a movie can shift page boundaries or change a filter/sort result. See [B1](B1-PAGINATION.md).

After a successful `SaveChanges` involving a Movie, Genre or MovieGenres join row, a scoped EF interceptor increments the shared version and evicts the movie-cache tag. It covers API POST/PUT/DELETE and the existing MVC/import/genre write paths without changing their controllers. Validation failures, failed saves and no-op saves do not invalidate. Invalidation completes after the save even if the caller disconnects. Existing models, relationships, database schema and write validation are unchanged.

All list and detail entries are invalidated together. This deliberately trades some detail-cache hits for reliable freshness when shared genres or list membership changes. An in-flight old GET may complete with its original snapshot, but can only write to an obsolete versioned key; subsequent GETs cannot reuse that stale response. A deterministic concurrency test covers this race.

The scope is one application process with normal `SaveChanges` calls. Direct SQL/`ExecuteUpdate`/`ExecuteDelete`, external database writers, or another app replica do not trigger the interceptor. Such writes require explicit invalidation; otherwise TTL bounds staleness. A multi-instance deployment would need a shared store and coordinated invalidation/versioning. No infrastructure changes were made for this local task.

## Video demonstration through FilmHub.Api.http

1. Start the application using the existing environment. Set `baseUrl` to its URL (existing file value retained; the `http` launch profile normally uses port 5103). Use a demonstration database for CRUD examples.
2. Find the **B9** section. Change `b9Run` to a new value. Run the cold GET, then the identical GET within 60 seconds. Inspect response headers: a cached response has `Age`, including `Age: 0`; a cold response has no `Age`. Record the actual elapsed times displayed by the HTTP client, with no promised ratio.
3. GET another query value: it has a separate cold entry. The original B9 examples use the legacy array format; use the B1 section for actual `skip`/`limit` pages.
4. POST the demo movie and copy its returned ID into `b9MovieId`. GET the previously warm list: no `Age`, new movie present. GET the detail twice to warm it.
5. PUT the movie. The next detail and each previously cached list variant show updated data without `Age`; a repeat is a hit again.
6. DELETE that demo movie. The detail immediately returns 404; the list excludes it. Do not use the ID of an unrelated existing movie.

Client timings can fluctuate due to JIT, transport, logging and machine load. The deterministic efficiency evidence is the number of executed SQL reader commands: cold/bypassed GETs execute SQL, whereas a warm GET executes none. No production query counter or diagnostic API is exposed.

## Reproducible measurements with no PostgreSQL writes

```powershell
dotnet build --no-restore
dotnet test --no-restore
dotnet test --no-restore --filter FullyQualifiedName~Demonstration_measures_uncached_cold_and_warm_queries --logger "console;verbosity=detailed"
```

The demonstration test emits **measured** SQL counts and elapsed milliseconds for an uncached GET, repeated uncached GET, cold cached GET and warm GET for both endpoints. Only the test's uncached baseline includes an Authorization header to exercise the default cache bypass; the public application has no authentication changes. No elapsed-time thresholds are asserted and there are no artificial delays. Each test boots the real application with SQLite in memory, with foreign keys and a SQL interceptor; real PostgreSQL is never used.

Coverage includes cache hits for both routes, query variation, successful CRUD invalidation across all warmed variants, rejected writes preserving valid cache entries, uncached 404s, sync/async shared-context writes, genre renames, join-only changes, failed saves and the in-flight GET race. Existing C5 and MVC/F1/F2/C7/Analytics regressions run in the same suite.

## Recorded local result, 2026-10-26

`dotnet test --no-restore`: **79 passed, 0 failed** (64 existing tests plus 15 B9 cases). `dotnet build --no-restore`: **0 errors**; the existing NU1901 warnings for NuGet.Packaging and NuGet.Protocol remain.

Actual measurements from the successful test run (SQLite in memory, TestServer; each cell shows executed SQL reader commands / elapsed milliseconds):

| Endpoint | Uncached baseline | Repeated baseline | Cold cache | Warm cache |
| --- | --- | --- | --- | --- |
| `/api/movies` | 1 / 76.785 ms | 1 / 10.384 ms | 1 / 2.303 ms | 0 / 0.303 ms |
| `/api/movies/1` | 1 / 38.944 ms | 1 / 10.514 ms | 1 / 2.735 ms | 0 / 0.400 ms |

These are observations from one run, not a production benchmark or a promised speed ratio. The first requests include startup/JIT costs. Repeat the measurement command above for your current machine. Raw results and per-test output are in the local ignored `obj/B9Verification/B9-tests.trx`, with console output in `obj/B9Verification/tests.log`.

Framework behavior: [ASP.NET Core OutputCache documentation](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output?view=aspnetcore-10.0).
