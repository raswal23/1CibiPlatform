# Serilog PostgreSQL Log Viewer Plan

## Goal

Persist structured Serilog events asynchronously in PostgreSQL and provide an authorized UI for searching and viewing them. PostgreSQL logging must not replace the existing console sink or make application requests depend on logging-database availability.

The planned flow is:

```text
1CibiPlatform backend
  -> Serilog
    -> existing JSON console sink
    -> bounded asynchronous PostgreSQL sink
      -> secured backend log-query API
        -> Blazor log viewer
```

## Current repository baseline

- Serilog is configured in `BackendAPI/API/APIs/ServiceConfig/ServiceConfiguration.cs`.
- The backend currently references `Serilog.AspNetCore` and `Serilog.Sinks.Console` in `BackendAPI/API/APIs/APIs.csproj`.
- The current pipeline writes JSON events to the console and uses `.Enrich.FromLogContext()`.
- `BackendAPI/BuildingBlocks/BuildingBlocks/Behaviors/LoggingBehavior.cs` creates a scope containing `UserId`, `Email`, and `FullName`, and logs MediatR request/response data.
- Each module registers the shared `LoggingBehavior<,>`, including ATS in `BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs`.
- ATS and the other modules already use PostgreSQL through the existing module infrastructure.

## Required module architecture

Platform logging is implemented as a dedicated module under:

```text
BackendAPI/Modules/PlatformLogging/
  BackgroundJobs/       retention hosted service
  Configuration/        typed logging options
  Data/Context/         PlatformLogging EF Core DbContext
  Data/Entities/        log event persistence entity
  Data/EntityConfiguration/ EF table, column, JSONB, and index mappings
  Data/Repository/      PostgreSQL query and retention persistence
  DTO/                  backend API contracts
  Features/Logs/Query/  Carter endpoints and MediatR query handlers
  Infrastructure/       bounded asynchronous PostgreSQL Serilog sink
  Path/                 module-owned YARP route definitions
  ServiceConfig/        MediatR, services, repository, and hosted-service registration
  Services/             application/query orchestration
```

Use ATS as the architecture reference. The read flow is:

```text
Carter endpoint
  -> MediatR query
    -> query handler
      -> platform log service
        -> platform log repository
          -> PostgreSQL
```

The module owns its YARP paths through `PlatformLogging/Path/PlatformLoggingPaths.cs` using `IReverseProxyModule`. Do not add these routes directly to gateway `appsettings.*.json`.

### EF Core and migration ownership

Follow the same split used by ATS:

- The module owns `PlatformLoggingDBContext`, `PlatformLogEvent`, entity configuration, repository, and database initialization extension.
- `PlatformLoggingDBContext.OnModelCreating` uses `ApplyConfigurationsFromAssembly`.
- The API project owns `PlatformLoggingDBContextFactory` because the API is the startup and migrations assembly.
- Generated migrations and the model snapshot live under `BackendAPI/API/APIs/Migrations/PlatformLogging/`.
- Runtime registration uses `UseNpgsql(..., options => options.MigrationsAssembly("APIs"))`.
- Environment database initialization calls `PlatformLoggingInitializeDatabaseAsync`, which uses `Database.MigrateAsync()`.
- The Serilog batching sink may use Npgsql directly for efficient inserts, but it does not create or alter the schema. EF Core migrations remain the only schema owner.

Generate future migrations with:

```powershell
dotnet ef migrations add <MigrationName> `
  --context PlatformLoggingDBContext `
  --project BackendAPI/API/APIs/APIs.csproj `
  --startup-project BackendAPI/API/APIs/APIs.csproj `
  --output-dir Migrations/PlatformLogging
```

### Code formatting and readability

PlatformLogging code must follow the readable style already used in ATS:

- Do not place multiple statements on one line.
- Use braces for conditional blocks, loops, and exception handlers, including short bodies.
- Break constructors and method signatures across lines when they have several parameters.
- Put each DI registration on its own line.
- Format LINQ pipelines with one operation per line when the query has filtering, ordering, projection, or pagination.
- Keep Carter route definitions vertically formatted with named arguments, matching `ATS/Path/ATSPaths.cs`.
- Keep SQL as an indented raw string with columns and values grouped on separate lines.
- Use descriptive names such as `logEvent`, `cancellationToken`, `connectionString`, and `exception`; avoid single-letter names outside trivial lambdas.
- Extract repeated or dense projection/serialization logic into named methods or expressions.
- Run the repository formatter after changing the module:

```powershell
dotnet format BackendAPI/Modules/PlatformLogging/PlatformLogging.csproj --no-restore
```

Formatting must improve readability without changing behavior. Build the PlatformLogging module and API after formatting.

## Important naming decision

Use the following structured fields:

- `Platform`: `1CibiPlatform` (the deployed unified platform)
- `Application`: the logical module, such as `ATS`, `Auth`, `PhilSys`, `AIAgent`, `CNX`, or `SSO`
- `SourceContext`: the typed logger category/class supplied by `ILogger<T>`
- `Environment`: Development, Testing, UAT, Sandbox, or Production

This preserves the requested `Application = "ATS"` behavior while still identifying the containing platform separately.

## Phase 1: Add module identification to structured logs

Update the shared `LoggingBehavior<TRequest, TResponse>` to derive `Application` from `typeof(TRequest).Namespace` and add it to the existing scope.

Expected mapping:

```text
ATS.*     -> ATS
Auth.*    -> Auth
PhilSys.* -> PhilSys
AIAgent.* -> AIAgent
CNX.*     -> CNX
SSO.*     -> SSO
otherwise -> Unknown
```

Conceptual scope:

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["Platform"] = "1CibiPlatform",
    ["Application"] = GetApplicationName(typeof(TRequest)),
    ["UserId"] = userId,
    ["Email"] = email,
    ["FullName"] = fullName
}))
{
    // Existing MediatR logging and next()
}
```

Because handlers and services execute inside `next()`, their normal `ILogger<T>` events inherit the same scope. No changes are required at each ATS log statement.

Also enrich all events globally with `Platform` and `Environment` in the Serilog configuration. Do not globally set `Application`, because one backend process hosts several logical applications/modules.

### Note: ATS Quartz jobs and non-MediatR execution

The automatic request-type detection applies only while code executes inside the MediatR pipeline.

For ATS Quartz jobs and other execution paths outside MediatR, add an ATS scope at the entry point:

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["Application"] = "ATS"
}))
{
    // Execute the ATS background operation
}
```

Apply this note to the current ATS Quartz entry points:

- `BackendAPI/Modules/ATS/BackgroundJobs/BulkSubmission/BulkSubmissionBackgroundJob.cs`
- `BackendAPI/Modules/ATS/BackgroundJobs/EmailNotification/EmailNotificationBackgroundJob.cs`
- `BackendAPI/Modules/ATS/BackgroundJobs/ApplicantSearchProjection/ApplicantSearchProjectionJob.cs`

Use the same boundary-scope approach for any future ATS hosted service, message consumer, scheduled task, or directly invoked operation that does not pass through MediatR.

## Phase 2: Define the PostgreSQL schema

Create and update the log table only through PlatformLogging EF Core migrations in the API project. Do not let the sink or repository issue `CREATE TABLE` or `ALTER TABLE` statements.

Suggested table shape:

```text
platform_log_events
- id                 bigint generated identity primary key
- occurred_at        timestamptz not null
- level              varchar(32) not null
- message_template   text
- rendered_message   text not null
- exception          text null
- properties         jsonb not null
- platform           varchar(100) not null
- application        varchar(100) not null
- environment        varchar(50) not null
- source_context     text null
- trace_id           varchar(64) null
- request_id         varchar(100) null
```

Use UTC timestamps. Keep uncommon structured properties in `jsonb`, while promoting frequently filtered fields to typed columns.

Initial indexes:

```sql
CREATE INDEX ON platform_log_events (occurred_at DESC, id DESC);
CREATE INDEX ON platform_log_events (application, occurred_at DESC);
CREATE INDEX ON platform_log_events (level, occurred_at DESC);
CREATE INDEX ON platform_log_events (trace_id, occurred_at DESC)
WHERE trace_id IS NOT NULL;
```

Avoid a JSONB GIN index until real query requirements justify its insert and storage cost.

## Phase 3: Configure asynchronous PostgreSQL logging

After confirming package compatibility with the backend target framework, add:

- A maintained PostgreSQL Serilog sink
- `Serilog.Sinks.Async`

Configuration requirements:

- Keep the existing JSON console sink.
- Wrap only the PostgreSQL sink in the async wrapper.
- Use a bounded buffer to prevent unlimited memory growth.
- Prefer batching if the selected PostgreSQL sink supports it.
- Use `blockWhenFull: false` to protect request latency; document that events can be dropped when saturated.
- Use a dedicated least-privilege writer credential.
- Store the connection string through the existing secret/environment configuration mechanism.
- Disable automatic schema creation outside local development.
- Flush Serilog during graceful shutdown with a finite timeout.
- Route Serilog `SelfLog` diagnostics somewhere other than the PostgreSQL sink to avoid recursive failures.

If guaranteed delivery becomes a requirement, an in-memory asynchronous sink is insufficient. Plan a durable broker or disk spool instead.

## Phase 4: Protect sensitive data before persistence

Review `LoggingBehavior` before sending its output to PostgreSQL. It currently destructures complete requests and responses, which may store excessive or sensitive data.

Required controls:

- Never log passwords, tokens, authorization headers, cookies, connection strings, API keys, or identity documents.
- Avoid full request and response bodies by default.
- Add explicit redaction or allowlisted logging DTOs for sensitive commands.
- Consider whether `Email` and `FullName` should be stored, pseudonymized, restricted, or removed.
- Limit exception/property visibility based on user authorization.

## Phase 5: Add a secured read API

Add read-only endpoints following the ATS Carter/MediatR/service/repository conventions:

```http
GET /platform-logging/logs
GET /platform-logging/logs/{id}
```

The list endpoint should support bounded filters for:

- UTC date range
- `Application`
- level
- source context
- trace ID or request ID
- constrained message search, only if required

Implementation requirements:

- Operations/admin authorization enforced by the backend
- Parameterized queries only
- Strict maximum date range and page size
- Cancellation tokens and database command timeouts
- Keyset pagination using `(occurred_at, id)` rather than large offsets
- Small list DTOs; retrieve exception and full properties from the detail endpoint
- Allowlist properties returned to the UI

The UI must never connect directly to PostgreSQL.

## Phase 6: Build the Blazor log viewer

Follow the existing UI routing, typed service, API-client, MudBlazor table, and authorization patterns.

First release:

- Newest-first table
- Timestamp, level, application/module, source, trace/request ID, and shortened message
- Date-range, application, and level filters
- Server-side keyset pagination
- Separate or expandable detail view for exception and approved structured properties
- Loading, empty, denied, and API-failure states
- Controlled polling only if refresh is required; defer live streaming

Render every stored message, exception, and property as untrusted plain text. Never render log content as raw HTML.

## Phase 7: Retention and operations

- Agree on a retention period, initially 30, 60, or 90 days.
- At moderate volume, delete expired records in small indexed batches.
- At high volume, partition by time and drop expired partitions.
- Monitor table/index growth, database CPU and I/O, queue capacity, dropped events, sink failures, and retention failures.
- Provide a configuration switch that disables only the PostgreSQL sink while retaining console logging.
- Logging-database failure must not make the API unhealthy or fail business requests.

## Verification checklist

1. Confirm console logging still works with PostgreSQL unavailable.
2. Verify ATS MediatR events contain `Platform = 1CibiPlatform` and `Application = ATS`.
3. Verify Auth, PhilSys, AIAgent, CNX, and SSO requests receive their correct application value.
4. Verify each ATS Quartz job explicitly adds `Application = ATS`.
5. Emit structured events and verify timestamp, level, message, exception, properties, source, trace ID, and request ID mappings.
6. Test Unicode, apostrophes, null properties, and bounded oversized exceptions.
7. Verify unauthorized users cannot call the log API or open the log viewer.
8. Verify filters and cursors do not duplicate or skip equal-timestamp records.
9. Verify sensitive fields are absent or redacted from both PostgreSQL and API responses.
10. Simulate PostgreSQL downtime, queue saturation, graceful shutdown, and forced termination; document observed event loss.
11. Measure request latency and database load with logging enabled.
12. Run `EXPLAIN` on log-list queries using realistic retained data volume.

## Recommended delivery order

1. Structured `Platform`/`Application` enrichment and ATS Quartz scopes
2. Sensitive-data review and redaction
3. Database schema and migration
4. Asynchronous PostgreSQL sink with console fallback
5. Sink integration and resilience tests
6. Secured read API
7. Blazor log viewer
8. Retention job, monitoring, performance validation, and rollout switch
