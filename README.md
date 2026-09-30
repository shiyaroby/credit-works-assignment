# CreditWorks Vehicle Management

A small full-stack application for managing vehicles and their weight-based categories.

- **Backend:** ASP.NET Core 10 Web API + Entity Framework Core 10 + SQL Server 2022 (Docker)
- **Frontend:** Angular 21 (standalone components, SCSS)
- **Tests:** xUnit (unit + integration)

Built for the CreditWorks Software Engineer take-home assignment.

---

## Prerequisites

| Tool           | Version                 | Notes                                         |
| -------------- | ----------------------- | --------------------------------------------- |
| OS             | macOS / Linux / Windows | Developed on macOS (Apple Silicon)            |
| .NET SDK       | 10.0+                   | `dotnet --version`                            |
| Node.js        | 20+ (via nvm)           | `nvm use 20`                                  |
| Angular CLI    | 21+                     | `npm i -g @angular/cli`                       |
| Docker Desktop | latest                  | Apple Silicon: enable _Rosetta for x86/amd64_ |
| EF Core CLI    | 10+                     | `dotnet tool install --global dotnet-ef`      |

---

## Setup

### 1. SQL Server (Docker)

```bash
docker run -d --name creditworks-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong@Passw0rd" \
  -e "MSSQL_PID=Developer" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

Wait ~20 seconds for SQL Server to initialize. Verify:

```bash
docker exec -it creditworks-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong@Passw0rd' -C -Q "SELECT @@VERSION"
```

> **Apple Silicon note:** the SQL Server image is x86_64. In Docker Desktop,
> enable **Settings → General → "Use Rosetta for x86/amd64 emulation"** before
> running the container.

### 2. Backend configuration

The connection string is stored in **.NET user-secrets**, never in source control:

```bash
cd backend/CreditWorks.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=CreditWorks;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
```

Two important details about the connection string:

- `localhost,1433` — **comma**, not colon (SQL Server TCP syntax).
- `TrustServerCertificate=True` — required by EF Core 7+ because the Docker
  image uses a self-signed certificate.

Trust the ASP.NET Core development certificate once:

```bash
dotnet dev-certs https --trust
```

### 3. Apply migrations

```bash
cd ../..
dotnet ef database update \
  -p backend/CreditWorks.Infrastructure \
  -s backend/CreditWorks.Api
```

> **Why not auto-migrate at startup?** `Program.cs` deliberately does not call
> `Database.Migrate()`. Auto-migration races when multiple instances start
> concurrently, applies schema changes without review, and has no rollback
> path. Applying migrations explicitly with `dotnet ef database update` is
> standard for local development and CI.

### 4. Run the API

```bash
cd backend/CreditWorks.Api
dotnet run --launch-profile https
```

API is available at `https://localhost:7293` (HTTPS) and `http://localhost:5293` (HTTP).
Swagger UI: `https://localhost:7293/swagger`.

### 5. Run the frontend

In a **second terminal tab**:

```bash
cd frontend/creditworks-ui
npm install
npm start
```

Open **http://localhost:4200**.

The Angular dev server proxies `/api/*` requests to `https://localhost:7293`
via `proxy.conf.json`, so the browser only ever talks to
`http://localhost:4200`. The API also has a CORS policy allowing
`http://localhost:4200` as a fallback for setups without the proxy
(e.g. calling the API directly from a browser script during debugging).

---

## Running the tests

```bash
dotnet test
```

Expected: ~63 test cases pass (xUnit counts each `[InlineData]` row). They
cover resolver boundaries, the range validator, the split/merge category
editor, vehicle validation, vehicle sorting, bulk replace, edit-conflict
handling, and API-level tests of the Section 6 behaviour. No SQL Server is
needed to run the tests; they use an in-memory database.

---

## Architecture

```
backend/
├── CreditWorks.Core/            # Domain — no EF, no HTTP dependencies
│   ├── Models/                  # Manufacturer, Vehicle, VehicleCategory, CategoryIcons
│   ├── Interfaces/              # ICategoryResolver
│   ├── Services/                # CategoryResolver, CategoryRangeEditor, VehicleSorter
│   └── Validation/              # CategoryRangeValidator, CategoryFieldValidator, VehicleValidator
├── CreditWorks.Infrastructure/  # EF Core + SQL Server
│   ├── Data/AppDbContext.cs
│   └── Migrations/
├── CreditWorks.Api/             # ASP.NET Core Web API
│   ├── Controllers/             # Vehicles, Categories, Manufacturers
│   ├── Contracts/               # Request/response DTOs
│   └── Middleware/              # Global exception handling
└── CreditWorks.Tests/           # xUnit unit + integration tests

frontend/creditworks-ui/
└── src/app/
    ├── core/                    # api.service.ts, models.ts
    ├── shared/                  # error-list component
    ├── vehicles/                # list + create
    └── categories/              # list + edit/create
```

**Dependency direction:** `Core ← Infrastructure ← Api`. The domain has no
knowledge of persistence or HTTP.

---

## Database design

### Tables

| Table               | Columns                                                    | Notes                       |
| ------------------- | ---------------------------------------------------------- | --------------------------- |
| `Manufacturers`     | Id, Name, IsActive                                         | Name has a unique index     |
| `Vehicles`          | Id, OwnerName, ManufacturerId, YearOfManufacture, WeightKg | WeightKg is `decimal(10,2)` |
| `VehicleCategories` | Id, Name, MinWeightKg, MaxWeightKg, IconName               | MaxWeightKg is nullable     |

### Key design decisions

1. **Vehicle.CategoryId is not stored.** A vehicle's category is **computed**
   at read time from its weight and the current category ranges. This
   guarantees the vehicle list always reflects the current configuration
   without a reconciliation step. The trade-off is that resolving category
   information requires loading the category table on every vehicle query —
   acceptable at this scale; a production system would cache the ranges.

2. **Manufacturers are a table, not an enum.** The assignment states the list
   may change; modeling it as a table with a seed migration means new
   manufacturers can be added without recompiling.

3. **Icons are named static assets.** Each category stores an `IconName` that
   maps to a file under `frontend/creditworks-ui/public/icons/`. The allowed
   names live in `CategoryIcons` (Core) and are validated on the server, so the
   API cannot store an arbitrary path. This is simple and cacheable. A production system would store icons as blobs or in
   object storage, with the DB storing a URL.

4. **Ranges are stored as `decimal(10,2)`.** Matches the vehicle weight
   precision, so boundary comparisons are exact.

---

## Category calculation

### Boundary rule

**A range is `[MinWeightKg, MaxWeightKg)` — minimum inclusive, maximum exclusive.**

A vehicle weighing **exactly 500.00 kg** resolves to **Medium**, not Light:

- Light: `[0, 500)` → 0 ≤ w < 500
- Medium: `[500, 2500)` → 500 ≤ w < 2500
- Heavy: `[2500, ∞)` → w ≥ 2500

The top category must have `MaxWeightKg = null` (unbounded).

This rule is enforced by `CategoryResolver.ResolveCategory` and is covered by
`CategoryResolverTests` with explicit assertions at 0, 499.99, 500.00, 2499.99,
2500.00, and 99999.

### No gaps, no overlaps

Every write to the category table runs `CategoryRangeValidator`, which orders
the ranges and verifies:

1. The lowest category starts at 0.
2. Adjacent ranges share a boundary (no gap, no overlap).
3. The highest category is unbounded.

Any violation is returned as a `400 Bad Request` with a list of human-readable
errors:

```json
{ "errors": ["Gap between 'Light' and 'Medium'."] }
```

### Editing operations

Because the set must always cover 0 kg → ∞, adding or removing a category on
its own needs a rule for what happens to the neighbouring range.
`CategoryRangeEditor` (Core, pure logic with no EF dependency) defines it:

| Operation          | Behaviour                                                                                                                                                                                                      |
| ------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Create**         | The new category's start weight must fall strictly inside an existing category. That category is split: it ends at the new start, and the new category takes over the upper part (including an unbounded top). |
| **Delete**         | The range is absorbed by the category below; deleting the lowest category extends the one above down to 0. The only remaining category cannot be deleted.                                                      |
| **Edit name/icon** | Always allowed.                                                                                                                                                                                                |
| **Edit range**     | Saved only if the result is still valid on its own. Otherwise the API returns `409 Conflict` with `suggestedAction: "bulk-edit"` and the UI offers "Edit all".                                                 |
| **Edit all**       | `PUT /api/categories/bulk` replaces the whole set in one `SaveChanges` call (one database transaction), validated as a whole.                                                                                  |

Every operation finishes by running `CategoryRangeValidator` on the resulting
set, so the invariants are enforced in one place. Field-level rules (name
length, known icon, weight bounds and precision) are in `CategoryFieldValidator`.

---

## Testing strategy

| Layer                                          | Tests                   | What they cover                                                                                                                                |
| ---------------------------------------------- | ----------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `CategoryResolver`                             | 8 theory cases + 1 fact | Boundary values, null for empty config                                                                                                         |
| `CategoryRangeValidator`                       | 7 facts                 | Gap, overlap, missing zero start, unbounded top, empty set                                                                                     |
| `VehicleValidator`                             | 10 facts                | Owner required, year range, weight > 0, ≤ 2 decimals                                                                                           |
| `VehicleSorter`                                | 12 facts                | Four sort fields × asc/desc, unknown-field fallback, case insensitivity, empty input                                                           |
| `Integration (CategoryChangeIntegrationTests)` | 1 fact                  | **Section 6** — category change re-categorizes existing vehicles                                                                               |
| `BulkCategoryUpdateTests`                      | 6 facts                 | Bulk replace (boundary shift, gap/overlap/empty rejection); edit-conflict 409; rename success                                                  |
| `CategoryRangeEditor`                          | 7 facts + 3 theory rows | Split (inside, at existing boundary, inside unbounded top); remove (middle, lowest, top, only, not found)                                      |
| `CategoryApiTests`                             | 8 facts                 | Section 6 via HTTP (bulk boundary change re-categorises a vehicle); create splits; delete merges; 404s; server-side icon and weight validation |

Business rules are unit-tested directly in the domain layer. The integration
and API tests use `WebApplicationFactory<Program>` with an InMemory EF Core
provider overriding the SQL Server registration (`ApiFactory` for the API
tests). Note that InMemory does not enforce decimal precision, foreign keys or
unique indexes, nor does it provide real transaction isolation.

**Note on EF Core 10 in tests:** `Program.cs` registers SQL Server. The
integration test's `ConfigureServices` removes all EF Core service descriptors
(matching both `DbContextOptions<T>` and the new `IDbContextOptionsConfiguration`
from EF Core 10) before re-registering the InMemory provider. Otherwise EF Core
throws "Only a single database provider can be registered."

---

## Assumptions

- **Year of manufacture:** between 1886 (first automobile) and current year + 1.
- **Weight:** positive, at most two decimal places, stored in kg.
- **Manufacturers:** the assignment's five are seeded; the table is designed to
  accept more.
- **No authentication.** Deliberately out of scope per the assignment brief
  (Section 16: _"Authentication and authorisation are not required unless you
  choose to implement them."_). In production, all write endpoints would
  require authorization.

---

## Known limitations

1. **Range edits on an existing category go through the bulk editor.**
   Moving one boundary on its own would leave a gap or overlap until the
   neighbour is adjusted too, so the single-category `PUT` returns
   `409 Conflict` with `suggestedAction: "bulk-edit"` when the range change is
   not valid alone, and the UI offers to open `/categories/bulk`. Renaming a
   category or changing its icon remains a normal single-row `PUT`. Create and
   delete work on their own (split and merge, described above).

2. **Create takes a start weight only.** `POST /api/categories` splits the
   category containing the start weight, so a bounded range such as
   `[1000, 1500)` takes two steps (create, then adjust via "Edit all") or one
   bulk save.

3. **No optimistic concurrency or explicit serializable transaction on
   category writes.** Each write is a single atomic `SaveChanges`, but two
   concurrent editors could validate against stale data. A `rowversion`
   column and/or a serializable transaction would close this; it is not a
   concern for a single-user demo.

4. **Icons are static assets.** Fine for a demo; would move to blob storage or
   a CDN in production.

5. **No paging on the vehicle list.** Acceptable for the current scale; would
   paginate once the vehicle count grows.

6. **Change detection workaround in the SPA.** Angular 21 uses zoneless change
   detection by default; the components use `ChangeDetectorRef.detectChanges()`
   after async state updates. An idiomatic future version would use signals
   (`signal()`, `computed()`) throughout, removing the manual calls.

---

## What I'd improve next

- **Angular signals** throughout the SPA to eliminate the manual
  `detectChanges()` calls.
- **Testcontainers** for the integration test — spin up a real SQL Server
  container instead of using the InMemory provider, catching provider-specific
  bugs (case-sensitivity, decimal precision, index behaviour).
- **Create with an explicit range.** Accept an optional maximum on
  `POST /api/categories` and split the host category into three parts when both
  bounds fall inside it, so a bounded range can be created in one step.
- **Upper-bound-only schema.** Store just each category's upper bound and derive
  the lower bound from the previous row, making gaps and overlaps impossible by
  construction rather than by validation.
- **Optimistic concurrency** (`rowversion` on `VehicleCategories`) and a
  serializable transaction around category writes.
- **Authentication** on write endpoints (e.g. JWT bearer).
- **Paging, filtering, and search** on the vehicle list.
- **Container image + docker-compose** to bundle API + SQL Server + SPA for
  one-command startup.

---

## Security considerations

- **Server-side validation is authoritative.** Client-side validation exists
  for UX only; the API rejects anything that doesn't satisfy the business
  rules.
- **SQL injection prevention.** EF Core parameterizes all queries; no raw SQL
  is used.
- **Secrets management.** The connection string lives in .NET user-secrets
  (`~/.microsoft/usersecrets/<id>/secrets.json`), never in source control.
  `appsettings.Production.json` is gitignored defensively.
- **Error responses.** Unhandled exceptions produce a generic JSON message
  (`{"error": "An unexpected error occurred."}`) — no stack traces leak to the
  client. The `ExceptionMiddleware` logs full details server-side.
- **CORS.** The API allows only `http://localhost:4200` (the Angular dev
  server). The policy is registered in all environments, so it would need to
  be made configurable for a real deployment.

---

## Repository layout

```
.
├── .vscode/                    # VS Code workspace config (committed)
├── .gitignore
├── README.md
├── CreditWorks.slnx
├── backend/
│   ├── CreditWorks.Api/
│   ├── CreditWorks.Core/
│   ├── CreditWorks.Infrastructure/
│   └── CreditWorks.Tests/
└── frontend/
    └── creditworks-ui/
```

---

## Running the application — quick reference

Three terminals, one SQL Server container:

| Terminal | Command                                                           | Purpose                 |
| -------- | ----------------------------------------------------------------- | ----------------------- |
| —        | `docker start creditworks-sql`                                    | SQL Server (start once) |
| 1        | `cd backend/CreditWorks.Api && dotnet run --launch-profile https` | API                     |
| 2        | `cd frontend/creditworks-ui && npm start`                         | SPA                     |
| Browser  | `http://localhost:4200`                                           | App                     |

Tests: `dotnet test` from the repo root.
