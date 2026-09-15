# ATS Audit Trail — Code Explanation

Companion to [`ats-audit-trail.md`](ats-audit-trail.md). That document explains *what* the feature
does and *why* the rules exist. This one exists so a developer can change the implementation
without opening every file cold — it walks the real call chains, names the exact method at each
hop, and quotes the code that carries the correctness.

Read it top to bottom once, then use it as a map: *"I'm changing X, what else touches it?"* is
answered by §9.

> **Read §0 first.** The design doc predates several later changes and is now wrong in eleven
> places. Two of them are load-bearing: the `Changes` column **is** masked for sensitive property
> names (the doc, the entity, the behaviour and a unit test all still say it is not), and `Area`
> is `"Web"` for almost every real command (the doc says `UserManagement`). Every claim below was
> verified against the working tree on branch `feature/Update-ReadMe-File`; where the two
> documents disagree, this one follows the code.

---

## 0. Where the design doc no longer matches the code

| # | `ats-audit-trail.md` says | The code actually does |
|---|---|---|
| **C1** | §1 "Deliberately not redacted … **this column holds historical government IDs and birthdates for 30 days**" | **False.** `AtsAuditChangeInterceptor.Describe` masks any property whose name is in `AtsAuditRedactor.SensitivePropertyNames` — `SSS`, `TIN`, `DOB`, `DateOfBirth`, `BirthDate` are all in it — storing `*** → ***`. Only *non*-listed columns hold real before/after values. See §2.8 |
| **C2** | §2 schema: `Area` = "Feature folder from the namespace: `UserManagement`" | `ResolveArea` returns the segment **immediately after `Features`**. Every real ATS command lives in `ATS.Features.Web.*` or `ATS.Features.PublicApi.*`, so `Area` is `"Web"` or `"PublicApi"`. The unit test asserts `"ThingManagement"` from a fake namespace with no `Web` segment, so it passes while production always records `"Web"`. See §2.4 |
| **C3** | §1 "`[SkipAudit]` marks a command as unrecorded. Only `AskAtsAssistantCommand` carries it — a conversational turn whose question text would bury the log in noise" | The attribute is still there, but the *reason* is wrong and the outcome is the opposite: assistant turns **are** audited. `AtsAssistantService.RecordAudit` writes its own entry with both question and answer, because the pipeline can only serialize the request. `SkipAuditAttribute`'s own XML doc still carries the stale "noise" rationale. See §2.13 |
| **C4** | §1 "A caller without the right reads an **empty page**, not a 403" | True for the two read paths. `ExportAuditTrailAsync` **throws `ForbiddenException`** (403) and the endpoint declares `.ProducesProblem(StatusCodes.Status403Forbidden)`. See §4.2 |
| **C5** | §1 "There are 19 such calls in ATS" (`ExecuteUpdateAsync`) | **31** call sites across `BackendAPI/Modules/ATS` (34 grep hits minus 3 comment mentions). The email-accounts and notification repositories added more since. See §2.8 |
| **C6** | §1 "ATS has 22 `ICommand<>` and 13 `IQuery<>` types" | **38** command records declare `: ICommand<` (39 grep hits minus the behaviour's own generic constraint) and **39** declare `: IQuery<`. The write/read split the doc relies on is unchanged; the numbers are not |
| **C7** | §1 "`StopAsync` closes the channel so a graceful shutdown **drains the backlog instead of losing it**" | Not what the code achieves. `StopAsync` calls `Complete()` and then `base.StopAsync`, which cancels the stopping token; the loop is suspended on `WaitToReadAsync(stoppingToken)`, faults with `OperationCanceledException`, and the catch swallows it and exits. There is no post-`Complete()` drain pass. See §7.1 |
| **C8** | §5 test counts: redactor 11, behaviour 14, service 9, integration 24 | Redactor **9**, behaviour **14** ✓, service **13**, and `AtsAuditRepositoryIntegrationTests` holds **20** methods including the four interceptor tests the doc counts separately. `AtsAuditWorkbookWriterTests` (8 methods) is not mentioned at all |
| **C9** | — | **NOT FOUND:** the whole Excel-export slice. `ExportAuditTrail` (endpoint, validator, handler, `AtsAuditWorkbookWriter`, `AtsAuditExportDTO`) is absent from the design doc; it appears only in `README.md:247`. See §4.2 |
| **C10** | — | **NOT FOUND:** the AI-assistant surface. `IAtsAuditService.GetRecentEntriesAsync`, `AtsAuditEntrySummaryDTO`, `AtsAuditQueryDTO`, and the two `[KernelFunction]`s `GetAuditSummaryAsync` / `SearchAuditEntriesAsync` in `AtsAssistantPlugin` are an entire second read path the design doc never mentions. See §5 |
| **C11** | §2 indexes: "`(OccurredAt DESC, AuditEntryId DESC)`" | The configuration does say `.IsDescending(true, true)` and the snapshot records `.IsDescending()`, but the generated migration emits `descending: new bool[0]`. The migration file alone does not tell you which direction the physical index has. See §7.8 |

Two behaviours exist in the code that the design doc does not describe at all: the **assistant
transcript export formatting** in `AtsAuditWorkbookWriter.DescribePayload` (§4.3) and the
**second audit producer** in `AtsAssistantService` (§2.13).

---

## 1. The data model

### 1.1 Entity — `BackendAPI/Modules/ATS/Data/Entities/AtsAuditEntry.cs`

A `sealed` POCO with no mapping attributes; everything is fluent (§1.2). The class doc states the
design rule the whole schema follows:

```csharp
/// <summary>
/// One state-changing ATS operation, recorded for accountability rather than debugging.
/// This is deliberately not a foreign-keyed row: the trail records who acted at that
/// moment, so it has to survive the user being deactivated, reassigned to another client,
/// or removed outright. That is also why the caller's role, client and super-admin status
/// are copied onto the entry instead of being joined at read time - a role change must not
/// rewrite history.
/// </summary>
public sealed class AtsAuditEntry
```

`Site` carries the one field that is *not* filled in by the request:

```csharp
	// The user's ATS site. Unlike the role and client it is not a claim, so it is resolved
	// from UserDetails by the drain rather than read in the request - see
	// AtsAuditDrainService.ResolveSitesAsync.
	public string? Site { get; set; }
```

and `Changes` carries the comment that §7.2 shows to be wrong:

```csharp
	// Unlike Payload these are NOT redacted: the point of a diff is the actual old value.
	// That makes this column as sensitive as the source data, which is why the whole
	// screen is platform-super-admin only.
	public string? Changes { get; set; }
```

### 1.2 EF configuration — `Data/EntityConfiguration/AtsAuditEntryConfiguration.cs`

Note the folder: **`Data/EntityConfiguration/`**. Picked up by
`modelBuilder.ApplyConfigurationsFromAssembly(typeof(ATSDBContext).Assembly);`
(`Data/Context/ATSDBContext.cs:37`); the `DbSet` is `public DbSet<AtsAuditEntry> AuditTrail { get; set; }`
at line 29 — **the property is named `AuditTrail`, the table is `"AuditTrail"` in schema `"ats"`**,
and the entity class is `AtsAuditEntry`. Three different names for one thing; every repository
query uses `_dbContext.AuditTrail`.

```csharp
		builder.ToTable("AuditTrail", "ats");

		builder.HasKey(x => x.AuditEntryId);

		// Version 7 ids are minted by the behaviour so an entry keeps the order it was
		// recorded in even though the drain writes it later.
		builder.Property(x => x.AuditEntryId)
			   .ValueGeneratedNever();
```

`ValueGeneratedNever()` is load-bearing: the id is minted in the request (§2.3), not at write time
in the drain, so ordering survives the queue.

The two JSON columns:

```csharp
		// jsonb rather than text so the payload can be queried directly when someone asks
		// "which action set this field", without a second migration later.
		builder.Property(x => x.Payload)
			   .HasColumnType("jsonb")
			   .IsRequired();

		// Same reasoning, and nullable: only tracked writes produce a diff.
		builder.Property(x => x.Changes)
			   .HasColumnType("jsonb");
```

`Payload` is `NOT NULL` and the entity initialises it to `"{}"`, so the column always holds valid
JSON — including the two marker objects `AtsAuditRedactor` substitutes (§2.5). `Changes` is
nullable, and null is *meaningful*: see §2.9.

Both indexes:

```csharp
		// Mirrors the screen's fixed (OccurredAt DESC, AuditEntryId DESC) ordering, so the
		// keyset page is an index scan rather than a sort.
		builder.HasIndex(x => new { x.OccurredAt, x.AuditEntryId })
			   .IsDescending(true, true);

		// The retention sweep deletes the oldest rows first and needs the ascending order.
		builder.HasIndex(x => x.OccurredAt);
```

Widths that the truncation logic in §2.3 must match: `Action` 120, `Area` 80, `Outcome` 20,
`FailureReason` 500, `UserEmail`/`UserFullName` 255, `Site` 100, `IpAddress` 64, `TraceId` 64.
`Site` is commented "Matches UserDetailsConfiguration's width for the column it is copied from."

### 1.3 Migrations — three, all under `BackendAPI/API/APIs/Migrations/ATS/`

Namespace `APIs.Migrations.ATS`. All three are purely additive; there is no destructive change.

| Migration | What it does |
|---|---|
| `20260907160321_AddAtsAuditTrailATSMigration.cs` | `CreateTable("AuditTrail", "ats")` with 16 columns, plus `IX_AuditTrail_OccurredAt` and `IX_AuditTrail_OccurredAt_AuditEntryId` |
| `20260907170224_AddSiteToAtsAuditTrailATSMigration.cs` | `AddColumn<string>("Site", …, "character varying(100)", maxLength: 100, nullable: true)` |
| `20260907172306_AddChangesToAtsAuditTrailATSMigration.cs` | `AddColumn<string>("Changes", …, "jsonb", nullable: true)` |

The keyset index as generated (lines 46-52):

```csharp
            migrationBuilder.CreateIndex(
                name: "IX_AuditTrail_OccurredAt_AuditEntryId",
                schema: "ats",
                table: "AuditTrail",
                columns: new[] { "OccurredAt", "AuditEntryId" },
                descending: new bool[0]);
```

See §7.8 for why that empty array is worth checking against a live database.

**Rows written before `20260907170224` and `20260907172306` have `Site = NULL` and
`Changes = NULL`.** Nothing backfills them — the before-values were never captured, and the site
was never resolved. The dialog renders `—` for the first and the "not captured for this action
type" note for the second (§6.4), so old rows are distinguishable from new ones only by date.

### 1.4 Vocabulary — `BackendAPI/Modules/ATS/Constants/AuditOutcome.cs`

The whole file:

```csharp
namespace ATS.Constants;

/// <summary>
/// Whether an audited command completed or threw. Public, like <see cref="TicketStatus"/>,
/// because the audit trail screen filters on this vocabulary.
/// </summary>
public static class AuditOutcome
{
	public const string Success = "Success";

	// The handler threw. The request still failed normally for the caller - the audit
	// entry records that the attempt was made, not that it was swallowed.
	public const string Failure = "Failure";

	public static readonly string[] All = [Success, Failure];
}
```

`public static class` of `public const string`, **not an enum** — exactly the shape of
`TicketStatus`. `All` exists so the query validator can check a caller-supplied filter (§3.2) and
so `AtsAuditService.NormalizeOutcome` can canonicalize one.

There is a **hand-synced UI duplicate** with a *different name*: `AuditActionOutcome` in
`UI/FrontendWebassembly/DTO/ATS/AtsAuditTrailDTO.cs`:

```csharp
// Mirrors ATS.Constants.AuditOutcome, which lives in the backend assembly and is not
// referenced by the UI project.
public static class AuditActionOutcome
{
	public const string Success = "Success";

	public const string Failure = "Failure";
}
```

The WASM project does not reference the ATS assembly, so nothing at compile time keeps these in
sync. Note the UI copy has **no `All`** — the UI cannot enumerate the vocabulary, it only compares
against two literals.

### 1.5 The `Changes` JSON shape — `BackendAPI/Modules/ATS/DTO/AtsEntityChangeDTO.cs`

```csharp
namespace ATS.Data.DTO;

/// <summary>
/// One property whose value a command changed.
/// </summary>
public sealed record AtsPropertyChangeDTO(string? From, string? To);

/// <summary>
/// The changed properties of one entity, as EF Core's change tracker saw them at
/// SaveChanges. Only modified properties appear - an edit that touched two fields records
/// two, not the whole row.
/// </summary>
public sealed record AtsEntityChangeDTO
{
	// The entity type name, e.g. "PackageDetails".
	public string Entity { get; set; } = string.Empty;

	// The primary key value, so the row can be identified. Composite keys are joined.
	public string? Key { get; set; }

	// Added / Modified / Deleted.
	public string State { get; set; } = string.Empty;

	public Dictionary<string, AtsPropertyChangeDTO> Changes { get; set; } = [];
}
```

Two things to know about how this is serialized. `AtsAuditBehavior.SerializeChanges` calls
`JsonSerializer.Serialize(changes)` with **no options**, so property names go into the `jsonb`
column in **PascalCase**. The UI reads them back with
`new JsonSerializerOptions { PropertyNameCaseInsensitive = true }` (§6.4), which is the only thing
making that work — a switch to camelCase policy on the write side would silently empty every
"What changed" panel rather than fail to compile.

Also note the folder asymmetry: `AtsEntityChangeDTO.cs` lives in `ATS/DTO/` but declares
`namespace ATS.Data.DTO`, matching `AuditTrailListDTO`. `AtsAuditSummaryDTO.cs` in the same folder
declares `namespace ATS.DTO`. Both are real; grep by namespace, not by path.

---

## 2. The write side — how one command becomes a row

Traced in full because it is the heart of the feature and the part with the most moving pieces.
The read side is §3.

```
POST /ats/adduser  (any ATS ICommand<> route)
  → YARP route                                       (Path/ATSPaths.cs, PathSet → /adduser)
    → Carter endpoint → sender.Send(AddUserCommand)                        [MediatR]
      → ValidationBehavior<TRequest,TResponse>       (outermost; throws ValidationException)
      → LoggingBehavior<TRequest,TResponse>
      → AtsAuditBehavior<TRequest,TResponse>.Handle  ← everything below is inside here
          ├─ Stopwatch.StartNew()
          ├─ await next()  → AddUserHandler → service → repository
          │                    └─ ATSDBContext.SaveChangesAsync()
          │                         └─ AtsAuditChangeInterceptor.SavingChangesAsync
          │                              ├─ HasUsableOriginals? else LoadOriginalsAsync (extra SELECT)
          │                              ├─ Describe(entry) → AtsEntityChangeDTO
          │                              └─ IAtsAuditChangeCollector.Add(changes)   [scoped]
          └─ Record(request, stopwatch, outcome, failureReason)
               ├─ reads ICurrentUser / IHttpContextAccessor / Activity.Current  (still in scope)
               ├─ AtsAuditRedactor.Redact(request)          → Payload
               ├─ SerializeChanges()  ← reads the collector → Changes (or null)
               └─ IAtsAuditWriter.TryEnqueue(entry)          [singleton channel, never blocks]
  ← HTTP response returns to the caller here; nothing has been written to the database yet

… later, on its own scope …
AtsAuditDrainService.ExecuteAsync (BackgroundService)
  → reader.WaitToReadAsync → fill batch (≤ BatchSize)
    → WriteBatchAsync
        → IServiceScopeFactory.CreateScope() → ATSDBContext
        → ResolveSitesAsync(dbContext, batch)   ← the one query per batch
        → dbContext.AuditTrail.AddRange(batch); SaveChangesAsync()
```

### 2.1 Pipeline position and the three filters — `Services/AuditTrail/AtsAuditBehavior.cs`

Registered third, in `ServiceConfig/ATSServiceConfiguration.cs:25-34`:

```csharp
		services.AddMediatR(config =>
		{
			config.RegisterServicesFromAssembly(assembly);
			config.AddOpenBehavior(typeof(ValidationBehavior<,>));
			config.AddOpenBehavior(typeof(LoggingBehavior<,>));

			// Last, so validation runs first: a request rejected as invalid never reached
			// a handler and must not be recorded as an action someone took.
			config.AddOpenBehavior(typeof(AtsAuditBehavior<,>));
		});
```

`AddOpenBehavior` registration order **is** pipeline order, first-registered = outermost. So the
nesting is Validation → Logging → Audit → handler. Two consequences worth being explicit about:

- A request that fails validation throws in `ValidationBehavior`, which is *outside*
  `AtsAuditBehavior`, so the audit behaviour never sees it. That is the guarantee the comment
  claims, and it is structural rather than asserted — there is no test that could fail if someone
  reordered these lines except by observing an absent audit row.
- `DurationMs` measures from *inside* Logging, so it excludes validation and logging time. It is
  handler-plus-inner-pipeline time, not request time.

`ValidationBehavior` (`BackendAPI/BuildingBlocks/BuildingBlocks/Behaviors/ValidationBehavior.cs`)
carries the constraint that makes the write/read split compile-time:

```csharp
public class ValidationBehavior<TRequest, TResponse>
    (IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
```

`AtsAuditBehavior` copies it:

```csharp
public class AtsAuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
	where TRequest : ICommand<TResponse>
```

MediatR only closes an open behaviour over a request whose type satisfies the constraint, so the
**39** `IQuery<>` slices in ATS — including all three audit-trail queries — **never enter this
pipeline**. Read the trail and you do not appear in it. That is a deliberate consequence for
`ExportAuditTrail` too, and it is the sharpest one in the feature: see §7.3.

`LoggingBehavior`, by contrast, is constrained `where TRequest : notnull, IRequest<TResponse>`, so
it *does* see queries. The asymmetry is why `LoggingBehavior` needs a runtime namespace check and
`AtsAuditBehavior` needs one only for the module filter.

There are **38** command records in ATS today declaring `: ICommand<` (**C6** — the design doc says
22), spread across `Features/Web/**` and `Features/PublicApi/**`.

The two static filters, resolved once per closed generic type (lines 47-51):

```csharp
	private static readonly bool IsAtsCommand =
		typeof(TRequest).Namespace?.StartsWith("ATS.", StringComparison.Ordinal) == true;

	private static readonly bool IsAudited =
		!typeof(TRequest).IsDefined(typeof(SkipAuditAttribute), inherit: false);
```

`static readonly` on a generic type means one evaluation per closed `TRequest` for the lifetime of
the process — not per request. Both answers are properties of the *type*, so that is safe, and it
is why neither can be overridden by configuration at runtime.

`IsAtsCommand` exists because `AddOpenBehavior` registers into the **one shared container** that
every module's `Add*MediaTR` writes to. The comment above it names the failure it prevents:

```csharp
	// AddOpenBehavior registers IPipelineBehavior<,> into the one shared container, and
	// every module calls its own Add*MediaTR against that same collection - so without
	// this namespace check the ATS behaviour also wraps Auth's LoginWeb, Logout and
	// IsAuthenticated commands. Those already go to PlatformLogging; this table is the
	// ATS trail. LoggingBehavior.GetApplicationName reads the root namespace the same way.
```

It is a **namespace string prefix**, so a command declared outside `ATS.` is silently skipped.
`AtsAuditBehaviorTestCommands.cs` keeps a fake in `Auth.Features.Login.Command.LoginWeb` purely to
prove the filter leaves it alone.

`IsAudited` uses `IsDefined(typeof(SkipAuditAttribute), inherit: false)`. `inherit: false` matters:
`SkipAuditAttribute` is declared `[AttributeUsage(AttributeTargets.Class, Inherited = false)]`
(`Services/AuditTrail/SkipAuditAttribute.cs`), so a derived command would not inherit the opt-out
even if the reflection call asked it to. Detection is on the **command record**, not the handler or
the endpoint.

Exactly one production command carries it (`Features/Web/AIAssistant/Command/AskAtsAssistant/AskAtsAssistantHandler.cs:9-10`):

```csharp
[SkipAudit]
public record AskAtsAssistantCommand(string Question) : ICommand<AskAtsAssistantResult>;
```

`ConfirmOrderDraftCommand` does **not** — verified, it is a bare
`public record ConfirmOrderDraftCommand(Guid DraftId) : ICommand<ConfirmOrderDraftResult>;`.

### 2.2 `Handle` — timing, and the rethrow

```csharp
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		if (!_options.Enabled || !IsAtsCommand || !IsAudited)
		{
			return await next();
		}

		var stopwatch = Stopwatch.StartNew();

		try
		{
			var response = await next();

			stopwatch.Stop();

			Record(request, stopwatch, AuditOutcome.Success, failureReason: null);

			return response;
		}
		catch (Exception exception)
		{
			stopwatch.Stop();

			// The attempt is recorded and the exception continues to the global handler,
			// so the caller still gets its normal error response. An audited failure is
			// exactly the case someone will come looking for later.
			Record(request, stopwatch, AuditOutcome.Failure, exception.Message);

			throw;
		}
	}
```

Three filters short-circuit in one `if`, and the short-circuit is a **pass-through**, not a block —
`await next()` runs either way. `_options.Enabled` is read per request (it is an instance field
bound from `IOptions<AtsAuditOptions>`), unlike the two static flags; but `IOptions<T>` is a
singleton snapshot, so flipping `AtsAudit:Enabled` needs a restart regardless.

The `catch` is unfiltered `catch (Exception exception)`, so `OperationCanceledException` from a
client disconnect is recorded as `Failure` with `FailureReason = "The operation was canceled."`.
That is a real row in the trail for an aborted request. Note the contrast with the drain (§2.11),
which *does* special-case cancellation.

`throw;` — bare, so the original stack trace reaches `CustomExceptionHandler`.

### 2.3 `Record` — every caller value read synchronously, inside the scope

```csharp
	private void Record(
		TRequest request,
		Stopwatch stopwatch,
		string outcome,
		string? failureReason)
	{
		try
		{
			// Every caller value is read here, inside the request scope. The drain runs on
			// its own scope long after the response was sent, where there is no
			// HttpContext and no claims principal to read.
			var entry = new AtsAuditEntry
			{
				AuditEntryId = Guid.CreateVersion7(),
				OccurredAt = DateTime.UtcNow,
				Action = ResolveAction(),
				Area = ResolveArea(),
				Outcome = outcome,
				FailureReason = Truncate(failureReason, 500),
				DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
				UserId = _currentUser.UserId,
				UserEmail = Truncate(_currentUser.Email, 255),
				UserFullName = Truncate(_currentUser.FullName, 255),
				AtsRoleId = _currentUser.AtsRoleId,
				AtsClientId = _currentUser.AtsClientId,
				IsPlatformSuperAdmin = _currentUser.IsPlatformSuperAdmin,
				IpAddress = ResolveIpAddress(),
				TraceId = Truncate(Activity.Current?.TraceId.ToString(), 64),
				Payload = AtsAuditRedactor.Redact(request),

				// Read after the handler ran: the interceptor fills the collector during
				// SaveChanges, so before this point there is nothing to read.
				Changes = SerializeChanges()
			};

			_auditWriter.TryEnqueue(entry);
		}
		catch (Exception exception)
		{
			// Nothing about recording an action may break the action itself. A broken
			// audit path is a logged defect, not a failed request.
			_logger.LogError(
				exception,
				"Failed to record an ATS audit entry for {Request}",
				typeof(TRequest).Name);
		}
	}
```

`Record` is `void`, not `async Task` — it cannot await anything, which is the structural reason
every value must be materialised here rather than in the drain. **`Site` is the only recorded field
absent from this initializer**; the drain fills it (§2.12).

Every `Truncate` length is a hand-copied duplicate of a `HasMaxLength` in §1.2. Nothing enforces
the pairing. Widen `FailureReason` to 1000 in the configuration and the behaviour will still cut at
500; narrow it to 200 and every over-long message becomes a `DbUpdateException` **inside the drain's
`SaveChangesAsync`**, which loses the whole batch (§2.11) and logs it as a persistence failure with
no hint that a column width caused it.

`ICurrentUser` (`BackendAPI/Modules/Auth/Shared/Implementations/CurrentUser.cs`) is
`IHttpContextAccessor`-backed. On a request thread it reads claims; on a Quartz or drain thread
`Principal` is null and every one of these resolves to null/false:

```csharp
	public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
	public Guid? UserId => ParseGuid(
		GetClaimValue(ClaimTypes.NameIdentifier, AuthClaimTypes.UserId));
	public bool IsPlatformSuperAdmin => PlatformRoleIds.Contains(Auth.Constants.PlatformRoleIds.SuperAdmin);
	public int? AtsClientId => ParsePositiveInt(GetClaimValue(AuthClaimTypes.AtsClientId));
	public int? AtsRoleId => ParsePositiveInt(GetClaimValue(AuthClaimTypes.AtsRoleId));
```

So a command dispatched from a background job **is** audited (the filter is on namespace, not on
auth) but records `UserId = null`, `IsPlatformSuperAdmin = false` and no email. The entry still
stands — `UserId` is nullable and `ResolveSitesAsync` skips nulls. This is the same reason
`OMSTicketingProcessorService` passes an explicit `changedByUserId` to the order-history factory;
the audit behaviour has no such escape hatch, because there is no parameter to pass one through.

`ResolveIpAddress` is the only use of the injected accessor:

```csharp
	private string? ResolveIpAddress() =>
		Truncate(
			_httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
			64);
```

That is the **peer address**, not `X-Forwarded-For`. Behind the YARP gateway it records the
gateway's IP for every browser request, so the column is close to useless for attributing an action
to a machine unless forwarded-header middleware is configured upstream. Nothing in this feature
configures it.

`TraceId` comes from `Activity.Current?.TraceId`, which is the same value Serilog writes to
`logging.log_events` — the join the entity comment promises.

### 2.4 `ResolveAction` and `ResolveArea` — and why `Area` is always `"Web"` (**C2**)

```csharp
	// "AddUserCommand" reads as "AddUser" on screen. The suffixes are the two naming
	// conventions the ATS slices actually use.
	private static string ResolveAction()
	{
		var name = typeof(TRequest).Name;

		foreach (var suffix in new[] { "CommandRequest", "HandlerRequest", "Command" })
		{
			if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
			{
				return name[..^suffix.Length];
			}
		}

		return Truncate(name, 120) ?? name;
	}
```

The suffix list is **ordered and first-match**, and the order matters: `"HandlerRequest"` must be
tried before `"Command"` only because neither is a suffix of the other, but `"CommandRequest"` must
come first or a name ending in it would trim to `…Command`. Real names it produces:
`AddUserCommand` → `AddUser`; `DownloadMultipleOrderRecordsHandlerRequest` → `DownloadMultipleOrderRecords`;
`EmailInvitationRequestCommand` → `EmailInvitationRequest`; `MarkAllNotificationsReadCommand` →
`MarkAllNotificationsRead`. A command named something else entirely (say `SendInvite`) falls
through to the raw type name.

The result is a **string with no vocabulary behind it**. `AtsAuditRepository` filters on
`entry.Action == action` by exact match, the UI's `Action` filter is free text, and the assistant's
tool description suggests `'ResendApplicationForm'` as an example. Rename a command record and its
history splits across two `Action` values with nothing to warn you.

```csharp
	// ATS.Features.UserManagement.Command.AddUser -> "UserManagement". The feature folder
	// is the closest thing the codebase has to a business area for a command.
	private static string ResolveArea()
	{
		var segments = typeof(TRequest).Namespace?
			.Split('.', StringSplitOptions.RemoveEmptyEntries)
			?? [];

		var featuresIndex = Array.IndexOf(segments, "Features");

		return featuresIndex >= 0 && featuresIndex + 1 < segments.Length
			? Truncate(segments[featuresIndex + 1], 80)!
			: "Unknown";
	}
```

The comment's example namespace does not exist. The real one is
`ATS.Features.Web.UserManagement.Command.AddUser` (verified at
`Features/Web/UserManagement/Command/AddUser/AddUserHandler.cs:1`), so
`segments[featuresIndex + 1]` is **`"Web"`**. Every one of the 38 commands sits under
`ATS.Features.Web.*` or `ATS.Features.PublicApi.*`, so the only two `Area` values production ever
writes are `"Web"` and `"PublicApi"`.

That is why the assistant's tool parameter is described as
`"Optional area filter, for example 'Web' or 'PublicApi'"` (`AI/AtsAssistantPlugin.cs`) — the plugin
author observed the real data. It is also why the `Area` column and the `Area` filter chip are near
useless as a business-area breakdown: the column reports the *trust boundary* the slice was filed
under, which is a different question.

The unit test does not catch this, because the fake is namespaced without the `Web` segment
(`Test/Test/BackendAPI/Modules/ATS.UnitTests/AtsAuditBehaviorTestCommands.cs`):

```csharp
namespace ATS.Features.ThingManagement.Command.AddThing
{
	public record AddThingCommand(string Name, string? SSS = null) : ICommand<ThingResult>;
```

and asserts (`AtsAuditBehaviorTests.cs:294-303`):

```csharp
		// ATS.Features.ThingManagement.Command.AddThing -> "ThingManagement".
		Assert.Equal("ThingManagement", Assert.Single(_recorded).Area);
```

Correct for the fake, unreachable for any real command. If you move a slice's folder, `Area`
changes with it and no test notices.

### 2.5 `Payload` — `Services/AuditTrail/AtsAuditRedactor.cs`

A `public static class`, documented as "Pure and static so the masking rules can be tested without
a database or a request."

```csharp
	public const string Mask = "***";

	// The marker stored instead of a payload that would be too large to keep. The entry is
	// still worth writing - who did what is the point, the body is supporting detail.
	public const string OversizedPayload = """{"_note":"Payload omitted: too large."}""";

	public const string UnserializablePayload = """{"_note":"Payload omitted: not serializable."}""";

	// A bulk upload command carries every row of a spreadsheet. Without a cap one action
	// could write a megabyte row, so an oversized payload is dropped rather than stored.
	public const int MaxPayloadCharacters = 8_000;
```

Both markers are valid JSON **objects**, which is why they can go into a `NOT NULL jsonb` column.

The name set — 22 entries, matched by **whole name**, case-insensitively:

```csharp
	private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"SSS",
		"SSSIDNumber",
		"TIN",
		"DOB",
		"DateOfBirth",
		"BirthDate",
		"HashToken",
		"Password",
		"NewPassword",
		"CurrentPassword",
		// Matched by WHOLE name, so "Password" above does not cover these. An SMTP app
		// password reaching the audit trail would be readable by anyone with audit access
		// and would outlive the row it came from.
		"AppPassword",
		"SmtpPassword",
		"EncryptedPassword",
		"Token",
		"AccessToken",
		"RefreshToken",
		"Signature",
		"SignatureData",
		"FileContent",
		"FileBytes",
		"Base64",
		"Base64Content"
	};
```

Whole-name matching is the constraint that makes this list a maintenance liability: `SmtpPassword`
is present because `Password` would not have covered it, and `SSSIDNumber` is present for the same
reason. A new command carrying a sensitive value as `NationalId` or `MothersMaidenName` is stored
in full and nothing in the pipeline notices.

The set is `private`; the only window onto it is:

```csharp
	/// <remarks>
	/// Exposed so the entity-change interceptor masks the same names this class masks in command
	/// payloads. Two lists would drift, and the one that drifted would leak silently - the
	/// interceptor writes column values straight from the change tracker, where an SMTP password
	/// is as readable as a display name.
	/// </remarks>
	public static bool IsSensitiveProperty(string propertyName) =>
		SensitivePropertyNames.Contains(propertyName);
```

Serialization options, with the reason they are fully qualified:

```csharp
	// Fully qualified: Quartz publishes its own JsonSerializerOptions, and both namespaces
	// are global usings in this module.
	private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
	{
		// The audit payload is read by a human in a detail panel, so cycles and unmapped
		// types must degrade to a note rather than throwing into the request path.
		ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
		MaxDepth = 32
	};
```

`Redact<TRequest>` serializes **as the declared type**, not as `object`:

```csharp
			// Serialized as the declared type: commands are records, and serializing them
			// as object would lose the derived properties.
			var json = JsonSerializer.Serialize(request, SerializerOptions);
```

then re-parses into a `JsonDocument` and rewrites it through `WriteRedacted`, which recurses into
objects and arrays and masks a sensitive name **whatever the shape of its value**:

```csharp
					// Masked whatever the shape of the value: a sensitive name holding an
					// object or an array must not have its contents written out either.
					if (SensitivePropertyNames.Contains(property.Name))
					{
						writer.WriteStringValue(Mask);
					}
```

The two-step serialize → parse → rewrite is deliberate: it means masking works on the *JSON
property names*, so it is unaffected by how the CLR type is shaped, and `AddUserCommand`'s
`IReadOnlyCollection<AddUserDTO>` is walked like anything else.

The catch is narrow:

```csharp
		catch (Exception exception) when (exception is JsonException or NotSupportedException)
		{
			// A payload that cannot be serialized must not stop the entry being recorded.
			return UnserializablePayload;
		}
```

Anything else (an `OutOfMemoryException` on a huge payload, say) escapes `Redact`, is caught by
`Record`'s outer `try`, and **the whole entry is lost** — not just the payload. That is the one
hole in "recording an action must never fail because of the shape of its payload".

Note the length cap is applied to the *redacted* text, so masking can itself push a payload over
8 000 characters (each masked value collapses to `"***"`, which usually shrinks it — but a wide
object of many short fields will not).

### 2.6 `Changes` — the interceptor, in `SavingChanges`

`Data/Interceptors/AtsAuditChangeInterceptor.cs`. A `sealed` `SaveChangesInterceptor` taking
`IAtsAuditChangeCollector` by constructor injection. Its class doc states the two design rules:

```csharp
/// Reads the change tracker in <c>SavingChanges</c>, not <c>SavedChanges</c>: once the
/// save completes EF marks entries Unchanged and the original values are gone.
///
/// This sees only <b>tracked</b> writes - the load-then-mutate-then-save pattern the
/// settings services use. <c>ExecuteUpdateAsync</c> issues SQL directly and never
/// populates the change tracker, so those commands record no field changes and the UI
/// says so rather than implying nothing changed.
```

Both the sync and async overrides funnel into one `CaptureAsync`. The sync one blocks:

```csharp
	public override InterceptionResult<int> SavingChanges(
		DbContextEventData eventData,
		InterceptionResult<int> result)
	{
		CaptureAsync(eventData.Context, CancellationToken.None)
			.GetAwaiter()
			.GetResult();

		return base.SavingChanges(eventData, result);
	}
```

See §7.5 — this is sync-over-async on a path that can issue a database round trip.

`CaptureAsync` walks the tracker once:

```csharp
		foreach (var entry in context.ChangeTracker.Entries().ToList())
		{
			// The audit table is written by the drain on its own context, but guard anyway
			// so an audit entry can never describe itself.
			if (entry.Entity is AtsAuditEntry)
			{
				continue;
			}

			if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
			{
				continue;
			}
```

The `AtsAuditEntry` guard is not redundant. The drain resolves `ATSDBContext` from a scope
(§2.11), and `AddDbContext` attaches the interceptor to **every** `ATSDBContext` — so the drain's
own `SaveChangesAsync` re-enters this interceptor with a batch of `Added` audit entries. Without
the guard, writing 100 audit rows would capture 100 `AtsAuditEntry` diffs into a collector nobody
reads, at best wasted work and at worst a recursive payload.

`.ToList()` snapshots the enumerator before the loop, because `LoadOriginalsAsync` mutates entry
state.

### 2.7 Detached updates — `HasUsableOriginals` / `LoadOriginalsAsync`

This is the regression the design doc describes at length, and the code carries the reason inline:

```csharp
			// A detached update - DbSet.Update() on an AsNoTracking() entity, which is how
			// the package, role and module repositories save - has no usable originals:
			// EF marks every property modified with OriginalValue equal to CurrentValue.
			// The stored row is then the only source of the before values, so it is read
			// back before the UPDATE overwrites it.
			if (entry.State == EntityState.Modified && !HasUsableOriginals(entry))
			{
				await LoadOriginalsAsync(entry, cancellationToken);
			}
```

The predicate (line 99):

```csharp
	// True when the tracker holds a real before-image: at least one modified property
	// whose original differs from its current value. A genuinely tracked edit always
	// does; a detached Update() never does.
	private static bool HasUsableOriginals(EntityEntry entry) =>
		entry.Properties.Any(property =>
			property.IsModified && !Equals(property.OriginalValue, property.CurrentValue));
```

Read the edge case in that definition: a **tracked** edit that sets a property to the value it
already has fails `HasUsableOriginals` too, and so pays the extra `SELECT`. The `SELECT` then
returns the same values, `Describe` finds no difference, and the entry is dropped by the
`change.Changes.Count == 0` guard below. Correct outcome, one wasted round trip.

The read-back (line 103):

```csharp
		try
		{
			// Reads the row as it stands in the database and copies it over the entry's
			// original values, so Describe below compares against what was really there.
			// One extra SELECT per detached edit, on the write path only.
			var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);

			if (databaseValues is not null)
			{
				entry.OriginalValues.SetValues(databaseValues);
			}
		}
		catch (Exception)
		{
			// The row may have been deleted concurrently, or the provider may not support
			// the lookup. The diff is then merely incomplete - it must not fail the save.
		}
```

The bare `catch (Exception)` with no logging is the one place in this feature that swallows a
failure silently. A diff that quietly records nothing looks identical, in the UI, to the
`ExecuteUpdateAsync` case — the dialog says "not captured for this action type" (§6.4), which would
be a lie. If you ever need to debug a missing diff, add a log here first.

The pattern being defended against is real and verifiable —
`Data/Repository/PackageManagement/ATSRepository.Packages.cs`:

```csharp
	public Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken) =>
		_dbcontext.PackageDetails.AsNoTracking().FirstOrDefaultAsync(package => package.PackageId == packageId, cancellationToken);

	public async Task<PackageDetails> EditPackageAsync(PackageDetails package, CancellationToken cancellationToken)
	{
		_dbcontext.PackageDetails.Update(package);
```

and the integration test that pins it (`AtsAuditRepositoryIntegrationTests.cs:385-431`) reproduces
exactly that shape and then asserts the thing that proves the diff is computed by value:

```csharp
		// The detached Update() flags every property as modified, so this is what proves
		// the diff is computed by value rather than from IsModified.
		change.Changes.Should().NotContainKey(nameof(RoleDetails.RoleName));
		change.Changes.Should().NotContainKey(nameof(RoleDetails.RoleId));
```

### 2.8 `Describe` — by value, and **masked** (**C1**)

```csharp
		foreach (var property in entry.Properties)
		{
			var name = property.Metadata.Name;

			// A sensitive column is still reported as having changed - that an SMTP password
			// was replaced is exactly what an audit reader needs - but neither value is kept.
			// The names come from AtsAuditRedactor so this and the command payloads mask the
			// same set; a second list here would drift and leak without anything noticing.
			var isSensitive = AtsAuditRedactor.IsSensitiveProperty(name);

			switch (entry.State)
			{
				// A create has no before value; recording "null -> x" for every column
				// would bury the few fields that matter, so only non-defaults are kept.
				case EntityState.Added when property.CurrentValue is not null:
					change.Changes[name] = new AtsPropertyChangeDTO(
						null,
						isSensitive ? AtsAuditRedactor.Mask : Format(property.CurrentValue));
					break;

				// Compared by value rather than trusting IsModified: a detached Update()
				// flags every property, so IsModified alone would report the whole row as
				// changed once the originals above are loaded.
				case EntityState.Modified
					when !Equals(property.OriginalValue, property.CurrentValue):
					change.Changes[name] = isSensitive
						? new AtsPropertyChangeDTO(AtsAuditRedactor.Mask, AtsAuditRedactor.Mask)
						: new AtsPropertyChangeDTO(
							Format(property.OriginalValue),
							Format(property.CurrentValue));
					break;

				// A delete has no after value. The original is what is worth keeping -
				// it is the only remaining record of the row.
				case EntityState.Deleted when property.OriginalValue is not null:
					change.Changes[name] = new AtsPropertyChangeDTO(
						isSensitive ? AtsAuditRedactor.Mask : Format(property.OriginalValue),
						null);
					break;
			}
		}
```

**This is the code that contradicts the design doc.** All three states consult
`AtsAuditRedactor.IsSensitiveProperty(name)`. `SSS`, `SSSIDNumber`, `TIN`, `DOB`, `DateOfBirth` and
`BirthDate` are in that set (§2.5), so a `PersonalDetails` edit records `"SSS": {"From":"***","To":"***"}`.
The design doc's §1 "Deliberately not redacted … this column holds historical government IDs and
birthdates for 30 days" is **false** for exactly the fields it names. What the column does hold
unredacted is everything *not* on that list — names, emails, mobile numbers, addresses, statuses,
package descriptions — which is still sensitive, just not in the way the doc claims.

Four places repeat the stale claim, and only the interceptor tells the truth:

| Location | What it says |
|---|---|
| `Data/Entities/AtsAuditEntry.cs` (the `Changes` property comment) | "Unlike Payload these are NOT redacted" |
| `AtsAuditBehavior.SerializeChanges` XML doc | "Deliberately not redacted: the value of a diff is the actual old value" |
| `ats-audit-trail.md` §1 and §6 | "this column holds historical government IDs and birthdates" |
| `AtsAuditBehaviorTests.Handle_ShouldNotRedactTheDiff` | asserts a real SSS survives into `entry.Changes` |

The unit test deserves its own note, because it looks like it disproves the above and does not:

```csharp
	[Fact]
	public async Task Handle_ShouldNotRedactTheDiff()
	{
		// Deliberate, and the reason this column is as sensitive as the source data: the
		// point of a diff is the actual old value. The payload is still masked.
		var behavior = BehaviorFor<AddThingCommand>();

		RequestHandlerDelegate<ThingResult> savesChanges = () =>
		{
			_changeCollector.Add([
				new AtsEntityChangeDTO
				{
					Entity = "PersonalDetails",
					State = "Modified",
					Changes = new Dictionary<string, AtsPropertyChangeDTO>
					{
						["SSS"] = new("1111111110", "2222222220")
					}
				}
			]);
			…
		Assert.Contains("1111111110", entry.Changes!, StringComparison.Ordinal);
		Assert.DoesNotContain("1111111110", entry.Payload, StringComparison.Ordinal);
```

It **hand-builds the diff and pushes it straight into the collector**, bypassing the interceptor
entirely. What it actually proves is the narrower and true statement: `SerializeChanges` does not
run the diff through `AtsAuditRedactor`. It says nothing about what the interceptor puts into the
collector in production, which is `*** → ***` for `SSS`.

Key formatting joins composite keys so one string identifies the row:

```csharp
		// Composite keys - UserDetails is (UserId, ModuleId) - are joined so the row is
		// still identifiable from one string.
		return keyValues is { Length: > 0 }
			? string.Join(" / ", keyValues)
			: null;
```

and every value is flattened to a string, round-trippable for dates:

```csharp
	// Everything becomes a string: the dialog renders these as text, and a single column
	// type keeps the stored shape stable whatever the property was.
	private static string? Format(object? value) => value switch
	{
		null => null,
		DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
		DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
		DateOnly dateOnly => dateOnly.ToString("O", CultureInfo.InvariantCulture),
		IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString()
	};
```

`"O"` (round-trip) is what lets a reader tell a `timestamptz` from a display string; note
`TimeOnly` is **not** in the list and falls through to `IFormattable`, which does not use `"O"`.

**`ExecuteUpdateAsync` is invisible to all of the above (C5).** There are **31** call sites in
`BackendAPI/Modules/ATS` — `OMSTicketingRepository` (5), `ATSRepository.EmailInvitations` (10),
`ATSRepository.BulkUploads` (4), `ATSRepository.Reports` (2), `ATSRepository.Packages` (2),
`ATSRepository.ApplicationForms` (2), `AtsNotificationRepository` (2), `AtsEmailAccountRepository`
(2), `PublicApiRepository` (1), `ATSRepository.DisputeOrders` (1). Commands reaching the database
through any of them record `Changes = null`.

The `Added`/`Deleted`/`Modified` skip and the empty-diff skip together produce the second null case:

```csharp
			var change = Describe(entry);

			// A Modified entry whose properties all happen to match is not worth a row.
			if (change.State == nameof(EntityState.Modified) && change.Changes.Count == 0)
			{
				continue;
			}
```

Note the guard is `Modified`-only. An `Added` entity whose every property is null produces an
`AtsEntityChangeDTO` with an empty `Changes` dictionary, which is kept and serialized as
`{"Entity":"X","Key":null,"State":"Added","Changes":{}}`.

### 2.9 The collector handoff — `Services/AuditTrail/AtsAuditChangeCollector.cs`

The interface doc explains why it is scoped and why it cannot move to the drain:

```csharp
/// <summary>
/// Carries the field-level changes of one request from the DbContext that observed them to
/// the audit behaviour that records them.
/// Scoped, because a request is the unit both ends agree on: the interceptor writes to it
/// inside SaveChanges, and the behaviour reads it once the handler returns. It cannot be
/// done in the drain - by then the DbContext is disposed and the original values are gone.
/// </summary>
```

and pins the multi-save behaviour:

```csharp
	/// <summary>
	/// Records what one SaveChanges call modified. Called once per SaveChanges, so a
	/// command that saves twice accumulates both.
	/// </summary>
	void Add(IEnumerable<AtsEntityChangeDTO> changes);
```

The implementation is a bare `List<>` with a cap and **no locking and no clear**:

```csharp
	// Capped so one command cannot accumulate an unbounded list. A bulk upload saves
	// hundreds of rows; past this point the entries stop being read by a human anyway,
	// and the payload already records what the command asked for.
	private const int MaxTrackedEntities = 50;

	private readonly List<AtsEntityChangeDTO> _changes = [];

	public IReadOnlyList<AtsEntityChangeDTO> Changes => _changes;

	public void Add(IEnumerable<AtsEntityChangeDTO> changes)
	{
		foreach (var change in changes)
		{
			if (_changes.Count >= MaxTrackedEntities)
			{
				return;
			}

			_changes.Add(change);
		}
	}
```

`List<T>` is not thread-safe. Scoped-per-request is normally enough, but a handler that fans out
concurrent work over one injected `ATSDBContext` — the shape `BulkSubmissionProcessorService` uses,
albeit with per-file scopes — would have two interceptor callbacks calling `Add` at once. See §7.7.

The cap is **per scope, not per SaveChanges and not per command**. A command that saves 60 entities
in six batches of ten records the first 50 and silently drops the rest, and the stored JSON gives
no indication it was truncated (unlike the payload's `OversizedPayload` marker).

And the behaviour's reader:

```csharp
	private string? SerializeChanges()
	{
		var changes = _changeCollector.Changes;

		if (changes.Count == 0)
		{
			return null;
		}

		var json = JsonSerializer.Serialize(changes);

		// Same cap as the payload: a bulk save must not write an unbounded row.
		return json.Length > AtsAuditRedactor.MaxPayloadCharacters
			? AtsAuditRedactor.OversizedPayload
			: json;
	}
```

Null, not `"[]"`, is the whole point of the column's nullability — the XML doc above it spells out
why:

```csharp
	/// Null rather than "[]" so the dialog can tell "nothing was captured" apart
	/// from "nothing changed".
```

The oversized case reuses **`OversizedPayload`**, whose text is `{"_note":"Payload omitted: too large."}`.
So an over-large *diff* is stored as an object saying "Payload omitted" in the `Changes` column.
That is what makes the dialog's second `NoChangesReason` branch reachable (§6.4): the UI tries to
deserialize `Changes` as a `List<AuditEntityChangeDTO>`, an object throws `JsonException`, the catch
returns `[]`, and because `Entry.Changes` is not null-or-whitespace the message becomes "This
action changed too many records to list here." Correct behaviour, arrived at through a
`JsonException` used as a control-flow signal.

### 2.10 The channel — `Shared/Implementations/AtsAuditWriter.cs`

```csharp
/// <summary>
/// The in-memory queue between an audited request and the database. Singleton, because the
/// queue has to outlive the request scope that writes to it.
/// The same shape PostgreSqlBatchingSink uses for platform logs: a bounded channel that
/// drops rather than blocks, so a slow or unreachable database degrades the audit trail
/// instead of the application.
/// </summary>
public sealed class AtsAuditWriter : IAtsAuditWriter
```

```csharp
		_channel = Channel.CreateBounded<AtsAuditEntry>(
			new BoundedChannelOptions(Math.Max(100, options.Value.BufferSize))
			{
				// One drain service reads it.
				SingleReader = true,

				// DropWrite over Wait: waiting would push database latency back onto the
				// request thread, which is the one thing this queue exists to prevent.
				FullMode = BoundedChannelFullMode.DropWrite
			});
```

`Math.Max(100, …)` floors the buffer at 100 — a misconfigured `AtsAudit:BufferSize: 1` does not
produce a one-slot queue. `SingleReader = true` is a promise the code makes to the runtime; it is
only true because exactly one `AtsAuditDrainService` is registered and it is the sole consumer.
Registering a second hosted service that reads `Reader` would be undefined behaviour, not a
detectable error.

`DropWrite` semantics are worth being precise about: **the newest write is the one dropped**, not
the oldest. `AtsAuditOptions.BufferSize`'s comment says "Full means the oldest write is dropped" —
that is `BoundedChannelFullMode.DropOldest`, and it is not what is configured. Under sustained
overload you lose the *most recent* actions and keep the backlog, which is the opposite of what an
audit trail wants and the opposite of what the options file documents.

```csharp
	public bool TryEnqueue(AtsAuditEntry entry)
	{
		if (_channel.Writer.TryWrite(entry))
		{
			return true;
		}

		// Logged rather than thrown. A dropped entry is a real gap in the trail, so it is
		// a warning, but it must not turn a successful command into a failed request.
		_logger.LogWarning(
			"The ATS audit queue is full; the entry for {Action} by {UserId} was dropped",
			entry.Action,
			entry.UserId);

		return false;
	}
```

`TryEnqueue`'s return value is **discarded by both callers** — `AtsAuditBehavior.Record` and
`AtsAssistantService.RecordAudit` both call it as a statement. Nothing counts drops except the log.

```csharp
	/// <summary>
	/// Stops the queue accepting entries, so the drain can finish the backlog and exit
	/// rather than waiting forever on a channel nobody will write to again.
	/// </summary>
	public void Complete() => _channel.Writer.TryComplete();
```

`Complete` is **not on `IAtsAuditWriter`** — the interface declares only `TryEnqueue`. That is
deliberate, and the drain's constructor comment says why:

```csharp
	// Takes the concrete writer, not IAtsAuditWriter: the interface deliberately exposes
	// only the enqueue side, so nothing running in a request scope can consume the queue.
	public AtsAuditDrainService(
		AtsAuditWriter auditWriter,
```

So the DI registration must expose both the concrete type and the interface (§8).

### 2.11 The drain — `BackgroundJobs/AuditTrail/AtsAuditDrainService.cs`

A `sealed` `BackgroundService`. Its class doc gives the reason it is not Quartz:

```csharp
/// A BackgroundService rather than a Quartz job: the other ATS jobs tick on a schedule to
/// find work already sitting in the database, whereas this is a continuous consumer of an
/// in-memory queue and should write as soon as there is something to write.
```

The loop (lines 30-64):

```csharp
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Enabled)
		{
			return;
		}

		var batchSize = Math.Max(1, _options.BatchSize);
		var batch = new List<AtsAuditEntry>(batchSize);
		var reader = _auditWriter.Reader;

		try
		{
			while (await reader.WaitToReadAsync(stoppingToken))
			{
				batch.Clear();

				while (batch.Count < batchSize && reader.TryRead(out var entry))
				{
					batch.Add(entry);
				}

				if (batch.Count == 0)
				{
					continue;
				}

				await WriteBatchAsync(batch, stoppingToken);
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// Shutting down.
		}
	}
```

`_options.Enabled == false` makes the service return immediately and **never read the channel
again**. The behaviour honours the same flag, so normally nothing is enqueued either — but
`AtsAssistantService` does not (§7.4).

The outer `while` awaits `WaitToReadAsync`; the inner one drains greedily up to `batchSize` without
awaiting, so a busy queue is written in full batches and a quiet one in batches of one. Latency is
therefore bounded by one batch, not by a timer.

`batch.Clear()` reuses one `List<>` across iterations — safe only because `WriteBatchAsync` is
awaited before the next `Clear()`, and `AddRange(batch)` copies references out. Do not make the
write fire-and-forget.

```csharp
	private async Task WriteBatchAsync(
		List<AtsAuditEntry> batch,
		CancellationToken cancellationToken)
	{
		try
		{
			using var scope = _scopeFactory.CreateScope();

			var dbContext = scope.ServiceProvider.GetRequiredService<ATSDBContext>();

			await ResolveSitesAsync(dbContext, batch, cancellationToken);

			dbContext.AuditTrail.AddRange(batch);

			await dbContext.SaveChangesAsync(cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			// Not retried: a failing batch retried forever would block every entry behind
			// it, and the loop must keep running whatever one batch does.
			_logger.LogError(
				exception,
				"Failed to persist {Count} ATS audit entries",
				batch.Count);
		}
	}
```

A fresh scope **per batch**, so the `DbContext` and its scoped `IAtsAuditChangeCollector` are new
each time. One batch failing loses up to `BatchSize` entries with only a count in the log — the
entries themselves are not written anywhere, so they are unrecoverable. This is where a column-width
mismatch (§2.3) or a Postgres outage shows up.

The `catch (OperationCanceledException) … { throw; }` rethrows so the outer loop's own catch handles
shutdown; note that a batch cancelled mid-`SaveChangesAsync` is also lost.

### 2.12 Site resolution — one query per batch

```csharp
	/// <summary>
	/// Fills in each entry's Site from UserDetails.
	/// Site is the one recorded field that is not a claim, so it cannot be read in the
	/// request the way the role and client are. Resolving it here costs one query per
	/// batch instead of a round trip on every audited request, which is the whole reason
	/// the write was moved off the request thread.
	/// Like the rest of the caller's details it is then denormalised onto the row, so a
	/// later transfer to another site does not rewrite history.
	/// </summary>
	private static async Task ResolveSitesAsync(
		ATSDBContext dbContext,
		List<AtsAuditEntry> batch,
		CancellationToken cancellationToken)
	{
		var userIds = batch
			.Where(entry => entry.UserId.HasValue)
			.Select(entry => entry.UserId!.Value)
			.Distinct()
			.ToArray();

		if (userIds.Length == 0)
		{
			return;
		}

		// UserDetails is keyed (UserId, ModuleId): one row per module grant, each carrying
		// the same Site. Grouped so any one of them answers for the user, the same
		// assumption OMSTicketingRepository's Take(1) makes.
		var sitesByUser = await dbContext.UserDetails
			.AsNoTracking()
			.Where(user => userIds.Contains(user.UserId))
			.GroupBy(user => user.UserId)
			.Select(group => new
			{
				UserId = group.Key,
				Site = group.Select(user => user.Site).FirstOrDefault()
			})
			.ToDictionaryAsync(row => row.UserId, row => row.Site, cancellationToken);

		foreach (var entry in batch)
		{
			// A user with no ATS UserDetails row - a platform super admin who was never
			// granted a module - simply has no site, and the entry still stands.
			if (entry.UserId is { } userId && sitesByUser.TryGetValue(userId, out var site))
			{
				entry.Site = site;
			}
		}
	}
```

`private static`, taking the context and batch as parameters — it holds no state, which is why the
integration tests can only exercise it by **copying the query** rather than calling it (§7.9).

The `GroupBy` is the whole trick: `UserDetails`' primary key is `(UserId, ModuleId)`, so a user with
five module grants has five rows. Without the grouping, a join would multiply each audit entry by
the grant count. `FirstOrDefault()` inside the projection picks an arbitrary one, on the stated
assumption that all of a user's grants carry the same `Site`. If a user is ever transferred to a new
site by updating only *some* of their `UserDetails` rows, this picks whichever row Postgres returns
first — nondeterministic, and not something any test would catch.

`Distinct()` before the `Contains` keeps the parameter list small; `userIds.Length == 0` short-circuits
so a batch of purely background-job entries costs no query at all.

### 2.13 The second producer — `Services/AIAssistant/AtsAssistantService.cs` (**C3**)

This is the part the design doc gets backwards. `AskAtsAssistantCommand` keeps `[SkipAudit]`, and
the handler file says why:

```csharp
// Still skipped by the PIPELINE, but no longer unaudited: AtsAssistantService writes its
// own entry instead.
//
// The pipeline only ever serializes the request, so an entry written here would record the
// question and lose the answer - and half a conversation is not a record of it. The service
// has both sides, so it records the exchange itself. See AtsAssistantService.RecordAudit.
[SkipAudit]
public record AskAtsAssistantCommand(string Question) : ICommand<AskAtsAssistantResult>;
```

`RecordAudit` is called from **both** sides of `AskAsync` — line 224 on success, line 247 in the
`catch` — and builds the entry by hand. The payload is a purpose-built record,
`DTO/AtsChatAuditPayloadDTO.cs`:

```csharp
			var payload = new AtsChatAuditPayloadDTO
			{
				// Stored verbatim. The audit redactor masks by PROPERTY NAME, which cannot
				// help with free prose - a question that happens to contain an SSS or TIN is
				// stored as typed. That is the accepted cost of a complete transcript, and
				// the reason the trail stays super-admin only.
				Question = Truncate(question, MaxAuditedTextLength) ?? string.Empty,
				Answer = Truncate(answer, MaxAuditedTextLength) ?? string.Empty,
				WasRefused = wasRefused,

				// Counts, not the rows themselves: candidate and audit data already live in
				// the tables this trail sits beside, and copying them into the payload would
				// spread that data further for no gain.
				OrderResultCount = orderResultCount,
				AuditResultCount = auditResultCount,
				StagedOrderDraft = stagedOrderDraft
			};
```

with `private const int MaxAuditedTextLength = 4_000;` at line 8 — a **second, different** text cap
from `AtsAuditRedactor.MaxPayloadCharacters = 8_000`, and 4 000 × 2 fields plus JSON overhead can
exceed it. The payload is written with `JsonSerializer.Serialize(payload)` and **no length check at
all**, so a full-length transcript goes into `jsonb` untruncated while a bulk-upload command is
capped at 8 000. Two producers, two policies.

This is also the one place where free prose defeats the redactor: `Question` is not a name in
`SensitivePropertyNames`, so a user who types their SSS number into the chat stores it in plaintext.
The detail dialog warns about exactly this (§6.4).

`Action` and `Area` are **string literals**, not the behaviour's resolvers:

```csharp
				// The same shape AtsAuditBehavior.ResolveAction produces, so this row reads
				// like every other one on screen.
				Action = "AskAtsAssistant",
				Area = "AIAssistant",
```

Note `Area = "AIAssistant"` — a **third** value that `ResolveArea()` can never produce, since the
command's namespace is `ATS.Features.Web.AIAssistant.Command.AskAtsAssistant` and would resolve to
`"Web"`. So the assistant's own rows are the only ones with a meaningful `Area`, and they got it by
hand.

`Changes = null` with the reason inline:

```csharp
				// A conversation writes nothing EF tracks; the one assistant action that
				// does - ConfirmOrderDraft - raises its own audited entry with its own diff.
				Changes = null
```

and the whole method is best-effort, mirroring `Record`:

```csharp
		catch (Exception exception)
		{
			_logger.LogError(
				exception,
				"Failed to record an ATS assistant audit entry for user {UserId}",
				_currentUser.UserId);
		}
```

Everything else — `AuditEntryId = Guid.CreateVersion7()`, the `ICurrentUser` reads, `IpAddress`,
`TraceId`, the same `Truncate` lengths — is a **line-by-line duplicate** of `AtsAuditBehavior.Record`.
There is no shared builder. Change one and the other drifts silently; §9 lists the pairs.

The literal `"AskAtsAssistant"` is then matched in two more places, in two more assemblies:
`AtsAuditWorkbookWriter.AssistantAction` (§4.3) and `AuditTrailDetailDialog.IsAssistantTranscript`
(§6.4). Four spellings of one string, none of them a constant shared with the others.

### 2.14 Retention — `BackgroundJobs/AuditTrail/AtsAuditRetentionService.cs`

A second `sealed` `BackgroundService`, "Modelled on `PlatformLogRetentionService`". Gated on both
flags:

```csharp
		if (!_options.Enabled || !_options.RetentionEnabled)
		{
			return;
		}

		var retentionInterval = TimeSpan.FromHours(
			Math.Max(1, _options.RetentionIntervalHours));

		using var timer = new PeriodicTimer(retentionInterval);

		do
		{
			try
			{
				await SweepAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				_logger.LogError(exception, "ATS audit trail retention failed");
			}
		}
		while (await timer.WaitForNextTickAsync(stoppingToken));
```

`do … while` means the **first sweep runs immediately at startup**, not after the first interval —
different from a `while (timer.WaitForNextTickAsync())` loop. On a 24-hour interval that is the
difference between pruning at boot and pruning a day later.

The batched delete:

```csharp
	private async Task SweepAsync(CancellationToken cancellationToken)
	{
		var batchSize = Math.Max(100, _options.RetentionBatchSize);
		var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _options.RetentionDays));

		int deleted;

		do
		{
			using var scope = _scopeFactory.CreateScope();

			var dbContext = scope.ServiceProvider.GetRequiredService<ATSDBContext>();

			var expiredIds = dbContext.AuditTrail
				.Where(entry => entry.OccurredAt < cutoff)
				.OrderBy(entry => entry.OccurredAt)
				.Select(entry => entry.AuditEntryId)
				.Take(batchSize);

			deleted = await dbContext.AuditTrail
				.Where(entry => expiredIds.Contains(entry.AuditEntryId))
				.ExecuteDeleteAsync(cancellationToken);

			if (deleted > 0)
			{
				_logger.LogInformation(
					"Deleted {Count} ATS audit entries older than {Cutoff}",
					deleted,
					cutoff);
			}
		}
		while (deleted >= batchSize && !cancellationToken.IsCancellationRequested);
	}
```

A fresh scope **per batch** — a long sweep does not hold one `DbContext` or one transaction open.
The `OrderBy(OccurredAt).Take(batchSize)` sub-query is what the plain ascending
`HasIndex(x => x.OccurredAt)` exists for; note the *delete* then goes through
`expiredIds.Contains(...)`, i.e. a `WHERE "AuditEntryId" IN (subquery)`, so the composite descending
index is not used here at all.

`Math.Max(1, RetentionDays)` floors retention at one day: `RetentionDays: 0` does not mean "keep
nothing", it means "keep one day". `Math.Max(100, RetentionBatchSize)` floors the batch at 100.

The loop continues `while (deleted >= batchSize)`, so a pass that deletes exactly `batchSize` rows
runs again. Correct, and it terminates because a short pass ends it.

`ExecuteDeleteAsync` bypasses the change tracker, so **retention deletes are themselves invisible to
the interceptor** — the trail does not record its own pruning. Nothing does; the only evidence is
the `LogInformation` line above.

### 2.15 Options — `Configuration/AtsAuditOptions.cs`

```csharp
public sealed class AtsAuditOptions
{
	public const string SectionName = "AtsAudit";

	public bool Enabled { get; set; } = true;

	// The in-memory queue depth. Full means the oldest write is dropped rather than the
	// request being held up - see AtsAuditWriter.
	public int BufferSize { get; set; } = 10_000;

	// How many entries the drain writes per SaveChanges.
	public int BatchSize { get; set; } = 100;

	public bool RetentionEnabled { get; set; } = true;

	public int RetentionDays { get; set; } = 30;

	public int RetentionIntervalHours { get; set; } = 24;

	public int RetentionBatchSize { get; set; } = 5_000;
}
```

Defaults match the design doc's table exactly. The `BufferSize` comment is the one that is wrong
(`DropWrite` drops the **newest**, §2.10). Bound in `AddATSInfrastructure` (§8); an absent
`AtsAudit` section is valid.

`RetentionDays = 30` has a **hand-synced duplicate in the UI**: `AuditTrailComponent.razor.cs`'s
`private const int RetentionDays = 30;` with the comment "Mirrors `AtsAuditOptions.RetentionDays`,
which lives in the backend assembly." Change one and the screen keeps promising the other.

---

## 3. The read side — one request end to end (`GetAuditTrail`)

```
GET /ats/getaudittrail?pageSize=10&outcome=Failure&searchTerm=…&startDate=…&endDate=…
  → YARP route "GetAuditTrail"        (Path/ATSPaths.cs:448-457, PathSet → /getaudittrail)
    → GetAuditTrailEndpoint           (Carter, MapGet "getaudittrail")
      → sender.Send(GetAuditTrailQueryRequest)                            [MediatR]
        → ValidationBehavior — NOT APPLIED (IQuery, not ICommand)
        → LoggingBehavior
        → GetAuditTrailHandler.Handle
          → IAtsAuditService.GetAuditTrailAsync
            → CanRead()                        [false → empty page, no 403, repo never called]
            → CursorCodec.Decode(cursor, 2)    [bad cursor → null → first page]
            → KeysetPage.Clamp(pageSize)
            → IAtsAuditRepository.GetAuditTrailPageAsync(…, take: pageSize + 1, …)
            → KeysetPage.Trim(rows, pageSize)
            → IAtsAuditRepository.CountAuditTrailAsync   [first page only]
      ← KeysetPaginatedResult<AuditTrailListDTO>
    ← Results.Ok(new GetAuditTrailEndpointResponse(result.AuditEntries))
```

### 3.1 Endpoint — `Features/AuditTrail/Query/GetAuditTrail/GetAuditTrailEndpoint.cs`

Note the path: **`Features/AuditTrail/`**, with no `Web/` segment — unlike the command slices, which
all live under `Features/Web/**`. These are queries, and the audit trail is the only ATS feature
folder that does not follow the `Web`/`PublicApi` trust-boundary split. (It also means `Area`
resolution would give `"AuditTrail"` for a command placed here — but there are none.)

```csharp
public record GetAuditTrailEndpointRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? Outcome = null,
	string? Action = null,
	string? Area = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);

public record GetAuditTrailEndpointResponse(KeysetPaginatedResult<AuditTrailListDTO> AuditEntries);
```

```csharp
		app.MapGet("getaudittrail", async (
			[AsParameters] GetAuditTrailEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
```

`[AsParameters]` binds eight query-string values to one record. The handler then constructs the
MediatR request **positionally**:

```csharp
			var query = new GetAuditTrailQueryRequest(
				request.Cursor,
				request.PageSize,
				request.Outcome,
				request.Action,
				request.Area,
				request.SearchTerm,
				request.StartDate,
				request.EndDate);
```

Eight positional arguments with six of them `string?`/`DateTime?`. Insert a parameter in the middle
of either record and this still compiles — it just binds the wrong values. That is the single most
fragile line in the slice.

Metadata: `.WithName("GetAuditTrail")`, `.WithTags("ATS")`,
`.Produces<GetAuditTrailEndpointResponse>(StatusCodes.Status200OK)`,
`.ProducesProblem(StatusCodes.Status400BadRequest)`, `.RequireAuthorization()`. **No
`.ProducesProblem(Status403)`** — because the read path never 403s (§3.3).

The response record's property name `AuditEntries` is the JSON wrapper the UI must match (§6.5).

### 3.2 Query, validator, handler — `GetAuditTrailHandler.cs`

All three in one file, per the project convention:

```csharp
public record GetAuditTrailQueryRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? Outcome = null,
	string? Action = null,
	string? Area = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null)
	: IQuery<GetAuditTrailQueryResult>;

public record GetAuditTrailQueryResult(KeysetPaginatedResult<AuditTrailListDTO> AuditEntries);
```

```csharp
public class GetAuditTrailQueryRequestValidator : AbstractValidator<GetAuditTrailQueryRequest>
{
	public GetAuditTrailQueryRequestValidator()
	{
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");

		// Cursor is deliberately unvalidated: cursors are opaque and a malformed one
		// self-heals to the first page rather than failing the request.
		RuleFor(x => x.Outcome)
			.Must(outcome => string.IsNullOrWhiteSpace(outcome)
				|| AuditOutcome.All.Contains(outcome, StringComparer.OrdinalIgnoreCase))
			.WithMessage($"Outcome must be empty or one of: {string.Join(", ", AuditOutcome.All)}.");

		RuleFor(x => x.Action)
			.MaximumLength(120)
			.WithMessage("Action cannot exceed 120 characters.");

		RuleFor(x => x.Area)
			.MaximumLength(80)
			.WithMessage("Area cannot exceed 80 characters.");
	}
}
```

`AuditOutcome.All` (§1.4) is why that array is `public`. The 120/80 maximums mirror the column
widths — a third hand-copied pair of numbers (§1.2, §2.3).

**This validator does not check the date range**, while `ExportAuditTrailQueryRequestValidator`
checks *only* the date range (§4.2). An inverted range on the paged read returns an empty page and
looks like "no activity"; the same range on the export returns 400. Two validators over the same six
filter fields, with disjoint rules.

Also note the unvalidated `SearchTerm`: it reaches `EF.Functions.ILike` as `$"%{searchTerm.Trim()}%"`
(§3.4), so `%` and `_` in a search term act as wildcards. Harmless here, but it is a raw
like-pattern injection.

The handler folds the query string into the shared `KeysetPaginationRequest` and does nothing else:

```csharp
	public async Task<GetAuditTrailQueryResult> Handle(
		GetAuditTrailQueryRequest request,
		CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm,
			request.StartDate,
			request.EndDate);

		var auditEntries = await _auditService.GetAuditTrailAsync(
			paginationRequest,
			request.Outcome,
			request.Action,
			request.Area,
			cancellationToken);

		return new GetAuditTrailQueryResult(auditEntries);
	}
```

`Outcome`, `Action` and `Area` travel *beside* the pagination record rather than inside it, because
`KeysetPaginationRequest` is shared BuildingBlocks infrastructure with only cursor/pageSize/search/
start/end.

### 3.3 `AtsAuditService.GetAuditTrailAsync` — the gate, the cursor, the count

`Services/AuditTrail/AtsAuditService.cs`, a `public sealed class`. Three constants at the top:

```csharp
	// What the AI assistant may read in one turn. Higher than the order search ceiling
	// because "list all the failures" is a normal audit question and ten rows reads as a
	// broken answer. Still bounded: the rows are rendered into a chat bubble and summarised
	// by a model, so anything larger belongs in the Excel export.
	private const int MaxAssistantEntries = 50;

	// A failure reason is diagnostic prose; the assistant only needs enough to say what
	// went wrong.
	private const int MaxFailureReasonLength = 200;

	// The trail is append-only and grows without limit, so an export is capped rather than
	// building an unbounded workbook in memory. Comfortably above a month of normal
	// activity, which is all the retention job keeps anyway.
	private const int MaxExportRows = 10_000;
```

The access gate, checked before anything else:

```csharp
	// Super admin only, and deliberately not IAtsAccessScopeResolver: this screen is not
	// client-scoped, because a trail the audited user can read is a weaker control. A
	// caller without the right reads an empty list rather than a 403, which is how every
	// other ATS list behaves.
	private bool CanRead()
	{
		if (_currentUser.IsAuthenticated && _currentUser.IsPlatformSuperAdmin)
		{
			return true;
		}

		_logger.LogWarning(
			"Audit trail read denied for user {UserId}: platform super admin is required",
			_currentUser.UserId);

		return false;
	}
```

and its use:

```csharp
		if (!CanRead())
		{
			return new KeysetPaginatedResult<AuditTrailListDTO>(
				Array.Empty<AuditTrailListDTO>(),
				null,
				0);
		}
```

The repository is never resolved-called on a denial, so the empty page costs no query —
`AtsAuditServiceTests.GetAuditTrailAsync_ShouldReturnAnEmptyPage_WhenTheCallerIsNotASuperAdmin`
verifies exactly that with `repository.Verify(…, Times.Never)`.

Note `IsAuthenticated && IsPlatformSuperAdmin`: `IsPlatformSuperAdmin` alone would be true for an
anonymous principal only if `PlatformRoleIds` were populated, which it cannot be without claims, so
the first check is belt-and-braces — but it is also the check that makes the log line meaningful.

Cursor decode:

```csharp
		// Cursor over the fixed (OccurredAt DESC, AuditEntryId DESC) ordering. An
		// undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);

		DateTime? afterOccurredAt = DateTime.TryParse(
			fields?[0],
			CultureInfo.InvariantCulture,
			DateTimeStyles.RoundtripKind,
			out var occurredAt)
			? occurredAt
			: null;

		Guid? afterEntryId = Guid.TryParse(fields?[1], out var entryId)
			? entryId
			: null;

		var hasSeek = afterOccurredAt.HasValue && afterEntryId.HasValue;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);
```

Two fields, `|`-joined and base64'd by `CursorCodec` (BuildingBlocks). `Decode` returns null on
malformed base64 or a wrong field count; each field is then *individually* parsed, so a cursor whose
timestamp is garbage but whose guid is fine yields `hasSeek == false` and restarts at page one. That
is the "self-heals" behaviour the validator comment promises. `DateTimeStyles.RoundtripKind` pairs
with the `"O"` format used to encode.

`CursorCodec`'s own doc names the trade:

```csharp
// A cursor that fails to decode (malformed base64, wrong field count) returns null
// and the caller silently falls back to the first page — a bad cursor is never an
// error. KISS trade-offs, by design: null and "" fields collapse on round-trip, a
// '|' inside a field value shifts the field count so the cursor decodes as null,
// and a stale cursor whose field values no longer parse degrades the same way.
```

The fetch, the trim, and the count:

```csharp
		var rows = await _auditRepository.GetAuditTrailPageAsync(
			hasSeek ? afterOccurredAt : null,
			hasSeek ? afterEntryId : null,
			pageSize + 1,
			normalizedOutcome,
			action,
			area,
			paginationRequest.SearchTerm,
			paginationRequest.StartDate,
			paginationRequest.EndDate,
			cancellationToken);

		var (page, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(
				page[^1].OccurredAt.ToString("O", CultureInfo.InvariantCulture),
				page[^1].AuditEntryId.ToString("D"))
			: null;

		long? totalCount = hasSeek
			? null
			: await _auditRepository.CountAuditTrailAsync(
				normalizedOutcome,
				action,
				area,
				paginationRequest.SearchTerm,
				paginationRequest.StartDate,
				paginationRequest.EndDate,
				cancellationToken);
```

Three things carry correctness here:

- **`pageSize + 1`.** `KeysetPage.Trim` needs the extra row to know a next page exists:
  ```csharp
  	// items must have been fetched with Take(pageSize + 1); the extra row only signals
  	// that a next page exists and is trimmed here.
  	public static (List<T> Items, bool HasMore) Trim<T>(List<T> items, int pageSize)
  ```
- **The cursor is built from the last *kept* row**, `page[^1]`, after trimming — not from the
  extra row. Using the wrong one would skip a row on the next page.
- **`totalCount` is computed only on the first page.** `hasSeek` is false exactly then. A `COUNT(*)`
  over an append-only table is the most expensive part of the request, so subsequent pages skip it
  and the UI keeps the number it already has.

`KeysetPage.Clamp` is `Math.Clamp(pageSize, 1, MaxPageSize)` with `MaxPageSize = 100` — the same 100
the validator enforces, in a fourth place.

The outcome filter is canonicalized before it reaches SQL:

```csharp
	// An unrecognised outcome would otherwise reach the repository as a literal filter and
	// silently return nothing; treat it as "no filter" instead.
	private static string? NormalizeOutcome(string? outcome) =>
		!string.IsNullOrWhiteSpace(outcome)
			&& AuditOutcome.All.FirstOrDefault(known =>
				string.Equals(known, outcome.Trim(), StringComparison.OrdinalIgnoreCase)) is { } matched
			? matched
			: null;
```

It returns the **canonical** spelling, so `"failure"` becomes `"Failure"` and the repository's
`entry.Outcome == outcome` comparison hits. This is defence in depth behind the validator: the
validator rejects an unknown outcome with 400, so `NormalizeOutcome`'s unknown-value branch is only
reachable from `GetRecentEntriesAsync` and `ExportAuditTrailAsync`, whose validators do not check
`Outcome` at all.

`Action` and `Area` get **no** such normalization — they are compared exactly, so a caller must
already know the canonical spelling (`"Web"`, per **C2**).

### 3.4 The repository — `Data/Repository/AuditTrail/AtsAuditRepository.cs`

The file opens with the registration rule:

```csharp
// Deliberately NOT cached, and no ATSCacheRepository decorator: the trail is append-only
// and the whole point of the screen is to show what just happened, so a cached first page
// would hide the most recent action. Same reasoning as OMSTicketingRepository.
public sealed class AtsAuditRepository : IAtsAuditRepository
```

One private query builder serves all three public methods, so the filters cannot diverge between
the page and the count:

```csharp
	private IQueryable<AtsAuditEntry> BuildRowsQuery(
		string? outcome,
		string? action,
		string? area,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate)
	{
		var query = _dbContext.AuditTrail.AsNoTracking();
```

Date handling stamps the kind before comparing against `timestamptz`, and treats `endDate` as
inclusive by adding a day and using `<`:

```csharp
		if (startDate.HasValue)
		{
			var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
			query = query.Where(entry => entry.OccurredAt >= start);
		}

		if (endDate.HasValue)
		{
			var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
			query = query.Where(entry => entry.OccurredAt < end);
		}
```

`DateTime.SpecifyKind` is not cosmetic: Npgsql throws on `Kind = Unspecified` against a
`timestamptz` column. The UI sends `"yyyy-MM-dd"`, which model-binds as `Unspecified`. The
assistant's `ResolveAuditPeriod` comment relies on this inclusive end: *"endDate is today because
the repository treats it as inclusive (it filters `OccurredAt < endDate + 1 day`)."*

The search, and why the payload is excluded:

```csharp
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var search = $"%{searchTerm.Trim()}%";

			// Who and what, not the payload: an ILIKE over a jsonb column would not use an
			// index and would let a search term reach masked content. Site is included so
			// the trail can be narrowed to one office.
			query = query.Where(entry =>
				EF.Functions.ILike(entry.UserFullName ?? string.Empty, search)
				|| EF.Functions.ILike(entry.UserEmail ?? string.Empty, search)
				|| EF.Functions.ILike(entry.Site ?? string.Empty, search)
				|| EF.Functions.ILike(entry.Action, search)
				|| EF.Functions.ILike(entry.Area, search));
		}
```

Five columns, none indexed for `ILIKE '%…%'`, so this is a sequential scan on every search. On a
30-day trail that is acceptable; it is the first thing that will hurt as the table grows.
`?? string.Empty` on the three nullable columns is what makes a null `Site` match nothing rather
than null-propagate the whole predicate.

Ordering and the seek, which the comment ties together:

```csharp
	// Newest first, unique AuditEntryId as the tiebreaker. ApplySeek must mirror this
	// expression exactly. Matches IX (OccurredAt DESC, AuditEntryId DESC).
	private static IQueryable<AtsAuditEntry> ApplyOrder(IQueryable<AtsAuditEntry> query) =>
		query
			.OrderByDescending(entry => entry.OccurredAt)
			.ThenByDescending(entry => entry.AuditEntryId);

	private static IQueryable<AtsAuditEntry> ApplySeek(
		IQueryable<AtsAuditEntry> query,
		DateTime afterOccurredAt,
		Guid afterEntryId) =>
		query.Where(entry => entry.OccurredAt < afterOccurredAt
			|| (entry.OccurredAt == afterOccurredAt
				&& entry.AuditEntryId.CompareTo(afterEntryId) < 0));
```

`ApplySeek` is the row-value comparison `(OccurredAt, AuditEntryId) < (after, afterId)` expanded into
`OR`/`AND`. `CompareTo` is how EF translates guid ordering to Postgres. **Change one and not the
other and paging silently repeats or skips rows** — `GetAuditTrailPageAsync_ShouldWalkEveryEntryExactlyOnce`
and `_ShouldNotRepeatRows_WhenTimestampsAreIdentical` are the two tests that would catch it, and both
need identical timestamps to be meaningful.

Because `AuditEntryId` is a **version-7** guid, its byte ordering is time-correlated, so the
tiebreaker is not arbitrary noise — entries created in the same instant still sort in creation
order.

The projection is a `static readonly Expression<…>` field rather than an inline lambda:

```csharp
	private static readonly Expression<Func<AtsAuditEntry, AuditTrailListDTO>> Projection =
		entry => new AuditTrailListDTO
		{
			AuditEntryId = entry.AuditEntryId,
			…
			Payload = entry.Payload,
			Changes = entry.Changes
		};
```

Used as `.Select(Projection)` in `GetAuditTrailPageAsync`. It copies **all 18 columns**, including
both JSON blobs, for every row on every page. `Changes` is not needed by the grid — only by the
detail dialog — so each page drags up to 8 KB × pageSize of diff JSON across the wire and into the
browser. There is no separate "detail" endpoint; the dialog renders from the row it already has.

`GetOutcomeCountsAsync` is the third public method and the reason `BuildRowsQuery` takes
`outcome` as a parameter it sometimes passes null for:

```csharp
	// One round trip for both buckets. The outcome filter is deliberately not applied:
	// the chips must keep showing every bucket's size while one of them is selected.
	public async Task<AuditOutcomeCountsDTO> GetOutcomeCountsAsync(
		…
		var grouped = await BuildRowsQuery(
				outcome: null,
				action,
				area,
				searchTerm,
				startDate,
				endDate)
			.GroupBy(entry => entry.Outcome)
			.Select(group => new OutcomeCountRow
			{
				Outcome = group.Key,
				Count = group.LongCount()
			})
			.ToListAsync(cancellationToken);

		return new AuditOutcomeCountsDTO
		{
			Success = CountFor(grouped, AuditOutcome.Success),
			Failure = CountFor(grouped, AuditOutcome.Failure),

			// Every row, including any outcome outside the known vocabulary, so the "All"
			// chip never silently under-reports.
			Total = grouped.Sum(entry => entry.Count)
		};
```

`Total` is the sum of **all** groups, not `Success + Failure`, so an unexpected `Outcome` value in
the table still counts toward "All". Defensive against a vocabulary change that never happened —
and, since `Outcome` is written from `AuditOutcome` constants in two places (§2.2, §2.13), one that
cannot happen without a code change.

---

## 4. The other two slices, as diffs from §3

Both under `Features/AuditTrail/Query/`, one `{Endpoint,Handler}.cs` pair per folder.

| Slice | Route | Service method | What differs from §3 |
|---|---|---|---|
| **GetAuditOutcomeCounts** | `GET getauditoutcomecounts` | `GetOutcomeCountsAsync(action, area, searchTerm, startDate, endDate, ct)` | **No cursor, no page size, no `Outcome` parameter at all.** Validator checks only `Action`/`Area` lengths. Denial returns `new AuditOutcomeCountsDTO()` (all zeros) rather than throwing. |
| **ExportAuditTrail** | `GET exportaudittrail` | `ExportAuditTrailAsync(…)` | **Throws `ForbiddenException`** instead of returning empty. Only the date range is validated. Returns `Results.File(…)`, not `Results.Ok(…)`. Cap of 10 000 rows. |

### 4.1 `GetAuditOutcomeCounts`

Request/response records:

```csharp
public record GetAuditOutcomeCountsQueryRequest(
	string? Action = null,
	string? Area = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null)
	: IQuery<GetAuditOutcomeCountsQueryResult>;

public record GetAuditOutcomeCountsQueryResult(AuditOutcomeCountsDTO Counts);
```

The endpoint's `.WithDescription` states the rule that `BuildRowsQuery(outcome: null, …)` implements:

```csharp
		.WithDescription(
			"Returns the number of successful and failed ATS actions for the current "
			+ "filters, so the audit trail chips keep showing every bucket's size while "
			+ "one of them is selected.")
```

Passing `Outcome` here would make the active chip's count collapse to its own filtered total — the
same trap `GetTicketStatusCounts` documents in the OMS feature. Here it is avoided structurally: the
parameter does not exist on the request.

The service denial path returns a zeroed DTO, so the chips read `0 / 0 / 0` for a non-super-admin
rather than erroring:

```csharp
		if (!CanRead())
		{
			return new AuditOutcomeCountsDTO();
		}
```

### 4.2 `ExportAuditTrail` — the 403 (**C4**, **C9**)

```csharp
		// A download is a stronger action than a screen read - the file leaves the system -
		// so this throws rather than returning an empty workbook. A caller who cannot read
		// the trail should be told, not handed a plausible-looking empty file.
		if (!CanRead())
		{
			throw new ForbiddenException("The audit trail is available to platform administrators only.");
		}

		var rows = await _auditRepository.GetAuditTrailPageAsync(
			afterOccurredAt: null,
			afterEntryId: null,
			MaxExportRows,
			NormalizeOutcome(outcome),
			action,
			area,
			searchTerm,
			startDate,
			endDate,
			cancellationToken);

		var content = AtsAuditWorkbookWriter.Write(rows);

		return new AtsAuditExportDTO
		{
			Content = content,
			FileName = BuildExportFileName()
		};
```

It reuses `GetAuditTrailPageAsync` with `take: MaxExportRows` and no seek — so the export is *the
first 10 000 rows of the same keyset query*, newest first. A trail larger than that exports its
newest 10 000 and says nothing about the truncation: the workbook has no "truncated" marker and the
endpoint reports 200. With 30-day retention that is unlikely to bind, but it is a silent cap.

The filename is server-generated and timestamped, with the reason inline:

```csharp
	// Timestamped rather than named from caller input, so a filter value can never reach
	// the Content-Disposition header.
	private static string BuildExportFileName() =>
		$"ats-audit-trail-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
```

The endpoint echoes the rule and adds the 403 to its declared problems:

```csharp
			// The filename is built by the service from a timestamp, never from caller
			// input, so a filter value can never reach the Content-Disposition header.
			return Results.File(
				result.Export.Content,
				ExcelContentType,
				result.Export.FileName);
```

```csharp
		.ProducesProblem(StatusCodes.Status403Forbidden)
		…
		.WithDescription(
			"Downloads the filtered audit trail as a styled Excel workbook: failed rows are "
			+ "highlighted and the cause of each failure has its own column, with a frozen "
			+ "header and auto-filter. Restricted to platform super admins - unlike the "
			+ "paged read, any other caller receives 403 rather than an empty file.")
```

The content type is a `private const string` in the endpoint:

```csharp
	private const string ExcelContentType =
		"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
```

and is **independently re-declared in the UI** as `AuditTrailComponent.razor.cs`'s
`private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";`
and again in `AIAssistantComponent.razor.cs`. Three literals, two assemblies, nothing enforcing them.

Its validator is the only one of the three that checks dates:

```csharp
public class ExportAuditTrailQueryRequestValidator
	: AbstractValidator<ExportAuditTrailQueryRequest>
{
	public ExportAuditTrailQueryRequestValidator()
	{
		// An inverted range is a caller mistake that would otherwise render an empty
		// workbook and look like "no activity".
		RuleFor(request => request)
			.Must(request =>
				!request.StartDate.HasValue
				|| !request.EndDate.HasValue
				|| request.StartDate.Value.Date <= request.EndDate.Value.Date)
			.WithMessage("The start date must be on or before the end date.");
	}
}
```

Note the asymmetry with the query record, which has **no default parameter values** while the
endpoint record does:

```csharp
public record ExportAuditTrailQueryRequest(
	string? Outcome,
	string? Action,
	string? Area,
	string? SearchTerm,
	DateTime? StartDate,
	DateTime? EndDate) : IQuery<ExportAuditTrailQueryResult>;
```

so it can only be constructed positionally — six arguments, four of them `string?`.

**Exporting the trail is not itself audited.** `ExportAuditTrailQueryRequest` is an `IQuery<>`, and
`AtsAuditBehavior` is constrained to `ICommand<>` (§2.1). The design doc argues at length that
report *downloads* are audited because "who pulled which report is exactly the access worth a
trail" — and the audit trail's own bulk download, which carries unredacted before/after values for
every non-listed column, leaves no trace. See §7.3.

### 4.3 `AtsAuditWorkbookWriter` — `Services/AuditTrail/AtsAuditWorkbookWriter.cs`

A `public static class`, separated from the service on purpose:

```csharp
/// Separate from <see cref="AtsAuditService"/> because presentation is not that service's
/// job - it owns the access rule and the query. This class knows nothing about who may read
/// the trail; it is handed rows that have already been authorised.
///
/// ClosedXML rather than CsvHelper (which the bulk subject export uses) because the point of
/// this file is that a reader can SEE the failures: a CSV cannot colour a row, freeze a
/// header or auto-filter a column.
```

Twelve columns, one sheet named `"Audit Trail"`. The header order is deliberate:

```csharp
		// Cause sits immediately after Outcome, so the reason a row is red is the next thing
		// the eye reaches rather than the last column off-screen.
		string[] headers =
		[
			"When (UTC)",
			"Action",
			"Area",
			"Outcome",
			"Cause of failure",
			"User",
			"Email",
			"Site",
			"Duration (ms)",
			"Details",
			"IP address",
			"Trace ID"
		];
```

**`Payload` and `Changes` are not columns.** The Details column is `DescribePayload(entry)`, a
human rendering of the payload only — so the field-level diff, the most sensitive thing in the
table, never reaches the workbook. Neither does `UserId`, `AtsRoleId`, `AtsClientId` or
`IsPlatformSuperAdmin`.

Two cells use `SetValue` rather than the `Value` property, and that is a security control:

```csharp
			// The whole reason this export exists. Written as text so a reason that starts
			// with '=' or '+' cannot be interpreted as a formula when the file is opened.
			sheet.Cell(rowNumber, 5).SetValue(entry.FailureReason ?? string.Empty);
```

`FailureReason` is an exception message, which can contain attacker-influenced text; `Details`
contains the payload, which for an assistant row is **whatever the user typed**. `SetValue` with a
`string` argument forces a text cell. `AtsAuditWorkbookWriterTests.Write_ShouldNotTreatLeadingEqualsAsAFormula`
pins it. Every other cell uses `.Value =` — safe, because those are timestamps, ints, or values the
system generated.

The assistant special case (**C3** again):

```csharp
	// The action name AtsAssistantService.RecordAudit writes. Matched on rather than
	// sniffing the payload's shape, which would misfire on any future command carrying a
	// "Question" field.
	private const string AssistantAction = "AskAtsAssistant";
```

```csharp
		if (!string.Equals(entry.Action, AssistantAction, StringComparison.OrdinalIgnoreCase))
		{
			return Truncate(entry.Payload, MaxDetailsLength);
		}

		try
		{
			using var document = JsonDocument.Parse(entry.Payload);
			var root = document.RootElement;

			var question = ReadString(root, "Question");
			var answer = ReadString(root, "Answer");

			var transcript = $"Q: {question}\n\nA: {answer}";

			// Surfaced as a line rather than left to be inferred from the answer's wording,
			// which may change.
			if (root.TryGetProperty("WasRefused", out var refused)
				&& refused.ValueKind == JsonValueKind.True)
			{
				transcript += "\n\n[refused as out of scope]";
			}

			return Truncate(transcript, MaxDetailsLength);
		}
		catch (JsonException)
		{
			// A payload that will not parse is still worth exporting as-is.
			return Truncate(entry.Payload, MaxDetailsLength);
		}
```

The three JSON property names — `"Question"`, `"Answer"`, `"WasRefused"` — are a **fifth** coupling
to `AtsChatAuditPayloadDTO`, by string. Rename a property on that record and the export silently
falls back to `ReadString` returning `string.Empty`, producing `Q: \n\nA: ` with no error.

The 30 000-character cap is a hard Excel limit, not a style choice:

```csharp
	// Excel refuses a cell over 32,767 characters. Kept well below so a long transcript
	// cannot fail the whole export.
	private const int MaxDetailsLength = 30_000;
```

Failure rows are tinted across the whole row:

```csharp
	// A failure is tinted across the whole row, not just its Outcome cell: someone scanning
	// a thousand rows for what went wrong should find them without reading a column.
	private static void StyleOutcome(…)
	{
		var isFailure = string.Equals(outcome, AuditOutcome.Failure, StringComparison.OrdinalIgnoreCase);
		…
		row.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#FDECEA");
		row.Style.Font.FontColor = ClosedXML.Excel.XLColor.FromHtml("#8C2F2A");

		sheet.Cell(rowNumber, 4).Style.Font.Bold = true;
	}
```

Layout — freeze, auto-filter, then pin the two wide columns:

```csharp
		// Freeze the header so it stays visible while scrolling a long trail, and switch on
		// auto-filter so a reader can narrow to Failure without writing a formula. Both are
		// the reason this is a workbook rather than a CSV.
		sheet.SheetView.FreezeRows(1);

		var lastRow = Math.Max(rowCount + 1, 1);

		sheet.Range(1, 1, lastRow, ColumnCount).SetAutoFilter();

		sheet.Columns().AdjustToContents();

		// AdjustToContents sizes a column to its longest value, which for Cause and Details
		// can be thousands of characters. Pin both and let the wrap handle the rest.
		sheet.Column(5).Width = CauseColumnWidth;
		sheet.Column(10).Width = DetailsColumnWidth;
```

`Math.Max(rowCount + 1, 1)` is what makes an empty export valid rather than throwing on a
zero-row range — `Write_ShouldProduceAValidWorkbook_WhenThereAreNoRows` covers it.

And the stream position, the last line of `Write`:

```csharp
		var content = new MemoryStream();

		workbook.SaveAs(content);

		// SaveAs leaves the position at the end; the endpoint streams from the start.
		content.Position = 0;

		return content;
```

Without that reset `Results.File` would emit a zero-byte download. `ColumnCount = 12` is a constant
that must match `headers.Length`; nothing checks it.

---

## 5. The AI-assistant surface (**C10**) — the second read path

Not in the design doc at all. Three pieces: a narrow projection, two kernel functions, and a chat
export button.

### 5.1 `GetRecentEntriesAsync` — the narrow projection

```csharp
		// The same gate the paged read uses. This method exists for the AI assistant, which
		// is available to every ATS role - so the check matters more here than anywhere
		// else in this file.
		if (!CanRead())
		{
			return [];
		}

		// Bounded regardless of what the caller asks for: this feeds a chat answer, and a
		// large page would blow out the model's context for no benefit.
		var clampedTake = Math.Clamp(take, 1, MaxAssistantEntries);

		// Null cursor values: the assistant always reads the newest entries and never
		// paginates, so it takes the first page of the existing keyset query.
		var rows = await _auditRepository.GetAuditTrailPageAsync(
			afterOccurredAt: null,
			afterEntryId: null,
			clampedTake,
			…
```

then projects to six fields and truncates the failure reason:

```csharp
				// Truncated because an exception message can run to thousands of characters
				// and is text an attacker can influence; the screen shows it in full.
				FailureReason = Truncate(row.FailureReason, MaxFailureReasonLength)
```

The DTO's doc explains what was left out and why — this is the feature's prompt-injection boundary:

```csharp
/// <c>Payload</c> and <c>Changes</c> are redacted but still rich JSON - the full command and
/// its field-level before/after values. Handing them to a language model widens the
/// prompt-injection surface (they contain free text a user typed) and the data-exposure
/// surface, for no gain over the audit screen, which already shows both in a detail panel
/// to the same super admins.
///
/// Identity columns (IpAddress, TraceId, AtsRoleId, AtsClientId) are omitted for the same
/// reason: the chat answers "what happened", and anything more forensic belongs on the
/// screen built for it.
```

(`DTO/AtsAuditSummaryDTO.cs`. The doc says "redacted but still rich JSON" — per **C1** the diff is
partly masked, but the point stands: it is free text either way.)

### 5.2 The two kernel functions — `AI/AtsAssistantPlugin.cs`

`GetAuditSummaryAsync` returns a *count* sentence; `SearchAuditEntriesAsync` returns *rows*. Both
gate on a field captured at construction:

```csharp
		_isPlatformSuperAdmin = currentUser.IsAuthenticated && currentUser.IsPlatformSuperAdmin;
```

```csharp
		// Same reasoning as GetAuditSummaryAsync: the access rule here is the platform role,
		// not the ATS client scope.
		if (!_isPlatformSuperAdmin)
		{
			return Array.Empty<AtsAuditEntrySummaryDTO>();
		}
```

So the check happens **twice** for one call — once in the plugin, once in `CanRead()` — with the
same rule expressed two ways. Both are needed only in the sense that neither trusts the other; the
service's is the one that matters, since `IAtsAuditService` is injectable anywhere.

`SearchAuditEntriesAsync`'s `[Description]` is unusually long, and the comment above it says why:

```csharp
	// The wording here is load-bearing. An earlier version said "use this AFTER a summary",
	// which the model read as "this is a follow-up step" - so a direct "list all the
	// failures" was answered from GetAuditSummary in prose and no table was ever produced,
	// because nothing populated LastAuditEntries. It now says to call this FIRST and names
	// the trigger words, so listing is the default rather than the second step.
```

Bounds:

```csharp
	private const int MaxAuditResults = 50;
	private const int MaxAuditDaysBack = 90;
```

`MaxAuditResults = 50` duplicates `AtsAuditService.MaxAssistantEntries = 50` in a second file; the
service clamps anyway, so the plugin's constant is only the value it asks for.

`MaxAuditDaysBack = 90` **exceeds `RetentionDays = 30`**. Asking the assistant for "the last 90
days" is accepted, clamped to 90, and silently returns only the 30 days that still exist. Nothing
tells the model or the user that the window is longer than the data.

Date derivation, with two reasons inline:

```csharp
	// The model is unreliable with relative dates, so it passes a day count and the period
	// is derived here. endDate is today because the repository treats it as inclusive
	// (it filters OccurredAt < endDate + 1 day).
	private static (DateTime StartDate, DateTime EndDate) ResolveAuditPeriod(int daysBack)
	{
		var clamped = ClampDaysBack(daysBack);
		var today = DateTime.UtcNow.Date;

		// clamped - 1 so "1 day" means today rather than today and yesterday.
		return (today.AddDays(-(clamped - 1)), today);
	}

	// A model that omits the argument sends 0; treat that as the default period rather
	// than an empty range that would silently return nothing.
	private static int ClampDaysBack(int daysBack) =>
		daysBack <= 0
			? DefaultAuditDaysBack
			: Math.Min(daysBack, MaxAuditDaysBack);

	// An empty string filter would reach the repository as a literal `= ''` and match
	// nothing; the model sometimes sends one instead of omitting the argument.
	private static string? NullIfBlank(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
```

The filters are then echoed back so the chat can offer an export of exactly those rows:

```csharp
		// Recorded so the chat can offer an export of exactly these filters. The clamped
		// day count is stored, not what the model asked for, so the export covers the same
		// period the user was shown.
		LastAuditQuery = new AtsAuditQueryDTO
		{
			DaysBack = ClampDaysBack(daysBack),
			Outcome = normalizedOutcome,
			Action = normalizedAction,
			Area = normalizedArea,
			SearchTerm = normalizedSearchTerm
		};
```

`AtsAuditQueryDTO`'s doc explains why this record has to exist at all:

```csharp
/// This exists because a language model cannot hand the browser a file - a download has to
/// be started by a real user gesture on the page. So the model produces the QUERY and the
/// page produces the file, and this record is what carries the query between them.
///
/// Echoed from the arguments the model actually passed rather than re-derived, so the
/// export can never silently widen what the user was shown.
```

### 5.3 Withholding on a refusal, and the chat export

`AtsAssistantService.AskAsync` withholds the audit rows on a refusal, for the same reason it
withholds the order table:

```csharp
			// Withheld on a refusal for the same reason the order table is: a jailbreak that
			// talks the model past its own refusal must not get a table out with it.
			var auditEntries = !plugin.WasRefusedAsOutOfScope && plugin.LastAuditEntries.Count > 0
				? plugin.LastAuditEntries
				: null;

			// Only offered alongside rows. An export button with no table above it would let
			// a user download a period they were never shown.
			var auditQuery = auditEntries is not null ? plugin.LastAuditQuery : null;
```

`auditQuery` is nulled unless `auditEntries` survived, so no button appears without a table.

The chat's export handler re-derives the date window in the UI
(`Component/ATS/AIAssistant/AIAssistantComponent.razor.cs:495`):

```csharp
			// The plugin clamped DaysBack before recording it, so this is the same period
			// the rows above came from.
			var startDate = DateTime.UtcNow.Date.AddDays(-(Math.Max(query.DaysBack, 1) - 1));

			var response = await AuditTrailService.ExportAuditTrailAsync(
				query.Outcome,
				query.Action,
				query.Area,
				searchTerm: null,
				startDate,
				DateTime.UtcNow.Date);
```

Two problems in those eight lines, both worth knowing before you touch this path:

1. **`searchTerm: null`.** `query.SearchTerm` is carried across the wire and then dropped. Ask
   "show me failures for Russel", get a five-row table, click Export, and the workbook contains
   every failure in the period for every user. The `AtsAuditQueryDTO` doc promises the export
   "can never silently widen what the user was shown" — this line does exactly that. See §7.10.
2. **The date arithmetic is a second implementation of `ResolveAuditPeriod`.** `-(DaysBack - 1)`
   from `DateTime.UtcNow.Date` is duplicated in C# in another assembly, with `Math.Max(…, 1)`
   standing in for `ClampDaysBack`'s default. If the day boundary passes between the answer and the
   click, the window shifts by one day.

---

## 6. Frontend — the same round trip from the browser

### 6.1 Page, guards, and how it becomes reachable

`UI/FrontendWebassembly/Component/ATS/AuditTrail/` holds six files: `AuditTrailComponent.razor`,
`.razor.cs`, `.razor.css`, `AuditTrailDetailDialog.razor`, `.razor.cs`, `.razor.css`.

```razor
@page "/s&i/ats/audittrail"
@namespace FrontendWebassembly.Component.ATS
@layout ATSLayout
@attribute [RequirePermission(6, 7)]
@attribute [RequireATSModule(15)]
@inherits CrudPageBase
@inject IAuditTrailService AuditTrailService
@inject IJSRuntime JS

<PageTitle>ATS - Audit Trail</PageTitle>
```

`RequirePermission(6, 7)` is **not** an audit-specific permission: application 6 is
`("s&i", "S&I", …)` (`ShareData/Auth/ApplicationList.cs:12`) and submenu 7 is
`("ats", "ATS", …)` (`ShareData/Auth/SubMenuList.cs:14`). Every ATS page carries that same pair,
including Ticketing Status. **The real UI gate is `[RequireATSModule(15)]`.**

Four separate things make the page reachable, and all four must agree:

**(a)** `BackendAPI/Modules/ATS/Constants/AtsModuleIds.cs`:

```csharp
	public const int TicketingStatus = 14;
	public const int AuditTrail = 15;
	public const int EmailAccountManagement = 16;
```

**(b)** `UI/FrontendWebassembly/ShareData/ATS/ModuleList.cs:32`:

```csharp
			{ 15, ("audittrail", "Audit Trail", Icons.Material.Filled.History) },
```

The `path` string must equal the last segment of the `@page` route, because `ATSLayout.CanAccessRoute`
resolves the URL segment back to a module id by matching it.

**(c)** The restriction list, with the reason for 15 spelled out:

```csharp
	// 15 (Audit Trail) is restricted for a different reason than the rest: a trail the
	// audited user can read is a weaker control, so only a platform super admin sees it.
	// The backend enforces the same rule independently.
	//
	// 16 (Email Accounts) is restricted because the accounts it manages are the credentials
	// every outbound invitation is sent through: …
	private static readonly int[] RestrictedAdministrationModuleIds = [6, 7, 8, 9, 11, 15, 16];
```

```csharp
	public static bool IsVisibleForAdministration(int moduleId, bool canViewAllModules) =>
		canViewAllModules || !RestrictedAdministrationModuleIds.Contains(moduleId);
```

**(d)** Navigation placement. `IsPrimaryNavigationModule` **excludes 15**:

```csharp
	// Modules that belong in the primary sidebar navigation rather than under Manage.
	public static bool IsPrimaryNavigationModule(int moduleId) =>
		moduleId <= 5 || moduleId == 12 || moduleId == 13 || moduleId == 14;
```

So Audit Trail renders in the collapsible **Manage → Settings** group (`ATSLayout.razor:58-88`,
the `settingsModules` branch), *not* in the primary sidebar next to Ticketing Status. The design
doc's "mirrors Ticketing Status" is true of the markup and false of the navigation.

And the component's own short-circuit, without which the attributes are inert:

```csharp
	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		// Without this guard the RequirePermission/RequireATSModule attributes are inert.
		if (!IsPageAuthorized)
		{
			return;
		}

		await RefreshCountsAsync();
	}
```

**Module 15 has no `UserDetails` grant backfill.** `Data/DataSeed/ATSInitialData.cs:481-489` seeds
the `ModuleDetails` row:

```csharp
	   new()
	   {
		   ModuleId = AtsModuleIds.AuditTrail,
		   ModuleName = "Audit Trail",
		   ModuleDescription = "User action audit trail module for ATS system.",
		   IsActive = true,
		   CreatedAt = DateTime.UtcNow,
		   UpdatedAt = DateTime.UtcNow
	   },
```

and `Data/Extensions/ATSDatabaseExtensions.cs` adds *missing* `ModuleDetails` rows incrementally:

```csharp
		await context.ModuleDetails.AddRangeAsync(
			initData.GetATSModules()
				.Where(module => !existingModuleIds.Contains(module.ModuleId)));
```

but the grant backfill is called for 13 and 14 only (lines 90-91):

```csharp
		await BackfillModuleGrantedWithNewOrderAsync(context, initData, AtsModuleIds.BulkUploads);
		await BackfillModuleGrantedWithNewOrderAsync(context, initData, AtsModuleIds.TicketingStatus);
```

This is **correct for 15 and would be a bug for any module 17**. Nobody is granted module 15
because nobody should be — access is by platform role, not by module grant, and `ATSLayout` gives a
super admin every module id unconditionally:

```csharp
		if (await AccessService.HasRoleAsync(RoleList.SuperAdminId))
		{
			_accessibleModuleIds = ModuleList.List.Keys.ToHashSet();
		}
```

Copy this screen's shape for a new *client-scoped* module and you must add the backfill call, or
existing databases will never see it.

### 6.2 Loading the table — `AuditTrailComponent.razor.cs`

The chips, reusing the shared status vocabulary:

```csharp
	// null is the "All" segment; the other two are the AuditOutcome vocabulary. Reusing
	// the shared dot modifiers: a successful action is "done" and a failed one is "error",
	// so this screen's chips match every other status board.
	private static readonly OutcomeSegment[] OutcomeSegments =
	[
		new OutcomeSegment(null, "All", "is-all"),
		new OutcomeSegment(AuditActionOutcome.Success, "Success", "is-done"),
		new OutcomeSegment(AuditActionOutcome.Failure, "Failure", "is-error")
	];
```

`AuditActionOutcome` is the hand-synced duplicate of `AuditOutcome` (§1.4).

The cursor state and the load callback:

```csharp
	private readonly CursorTableLoader<AuditTrailListDTO> _auditLoader = new();
```

```csharp
	private async Task<TableData<AuditTrailListDTO>> LoadAuditEntriesAsync(
		TableState state,
		CancellationToken cancellationToken)
	{
		// Every input that invalidates the keyset walk must be in the signature.
		var signature = string.Join(
			'|',
			_activeOutcome,
			_searchString,
			_dateRange?.Start?.ToString("yyyy-MM-dd"),
			_dateRange?.End?.ToString("yyyy-MM-dd"));

		var tableData = await LoadCursorPagedDataAsync(
			_auditLoader,
			state,
			signature,
			(cursor, pageSize) => AuditTrailService.GetAuditTrailAsync(
				cursor,
				pageSize,
				_activeOutcome,
				action: null,
				area: null,
				_searchString,
				_dateRange?.Start,
				_dateRange?.End));

		// The chips track the same search/date filters as the table, so they refresh with
		// it rather than drifting out of step.
		await RefreshCountsAsync();

		return tableData;
	}
```

The `signature` is the same keyset-invalidation mechanism the ticketing board uses: **a filter
missing from that join continues the old walk with the new filter applied**. Note `action` and
`area` are hardcoded `null` — the screen exposes no UI for either, so neither needs to be in the
signature. Add an Area dropdown and you must add it in *both* places.

Counts are refreshed at the end of every table load, and a failed count does not blank the table:

```csharp
			// A failed count must not blank the table that just loaded successfully; the
			// previous chip values stay on screen and the snackbar explains why.
			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			_counts = response.Data;
```

`_isLoadingCounts` is a re-entrancy guard set in `RefreshCountsAsync`; because the table load
awaits it and the chips `disabled="@_isLoadingCounts"`, the buttons lock while a load is in flight.

Filter changes reset MudTable's page in step with the loader:

```csharp
		// A changed filter starts a new keyset walk; keep MudTable's page in sync with the
		// loader's reset-to-first-page or the pager shows a stale page.
		if (_auditTable?.TableRef is not null)
		{
			_auditTable.TableRef.CurrentPage = 0;
		}
```

The table declares `ColumnCount="7"` and the header has exactly seven `MudTh` — When, User, Action,
Area, Outcome, Duration, Details. Unlike the ticketing board there is **no `Select` column**,
because there is no bulk action on this screen.

Time is rendered twice, relative over absolute:

```csharp
	private static string FormatRelative(DateTime value)
	{
		var elapsed = DateTime.UtcNow - DateTime.SpecifyKind(value, DateTimeKind.Utc);

		// A clock skew between the browser and the server can make a fresh entry look like
		// it arrived in the future; treat anything negative as "just now".
		if (elapsed < TimeSpan.FromMinutes(1))
		{
			return "just now";
		}
```

`DateTime.SpecifyKind(value, DateTimeKind.Utc)` appears in both `FormatRelative` and
`FormatAbsolute`, because `OccurredAt` deserializes from JSON without a `Kind`. Miss one and the
time shifts by the local offset.

The empty state varies by active chip:

```csharp
	private string EmptySubtitle => _activeOutcome switch
	{
		AuditActionOutcome.Success => "Nothing completed in this period. Check the Failure view.",
		AuditActionOutcome.Failure => "Every action in this period completed. Nothing needs attention.",
		_ => $"Changes made in ATS appear here within seconds and are kept for {RetentionDays} days."
	};
```

`RetentionDays = 30` here is the hand-synced duplicate of `AtsAuditOptions.RetentionDays` (§2.14).

### 6.3 Export from the board

```csharp
	private async Task ExportAuditTrailAsync()
	{
		if (_isExporting)
		{
			return;
		}

		_isExporting = true;

		try
		{
			var response = await AuditTrailService.ExportAuditTrailAsync(
				_activeOutcome,
				action: null,
				area: null,
				_searchString,
				_dateRange?.Start,
				_dateRange?.End);

			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			var fileBytes = await response.Data.Content.ReadAsByteArrayAsync();

			var fileName =
				response.Data.Content.Headers.ContentDisposition?.FileName?.Trim('"')
				?? "ats-audit-trail.xlsx";

			await JS.InvokeVoidAsync("downloadFile", fileName, ExcelContentType, fileBytes);

			Snackbar.Add("Audit trail exported.", Severity.Success);
		}
		finally
		{
			_isExporting = false;
			await InvokeAsync(StateHasChanged);
		}
	}
```

The XML doc above it justifies filtering the export at all:

```csharp
	/// Filtered on purpose, unlike the bulk subject export: that file is named after one
	/// upload and always means the whole of it, whereas this screen IS its filters - a
	/// workbook that silently ignored the active outcome chip and date range would not be
	/// the thing the user is looking at.
```

Unlike the chat's version (§5.3), **this one passes `_searchString`**. Same service method, two
call sites, different argument sets.

`_isExporting` is the double-click guard; markup adds `disabled="@(_isExporting || _counts.Total == 0)"`
so the button is also inert on an empty trail. `response.ErrorDetail` is the **only** path by which
the 403 message from §4.2 reaches the user — the service reads it via
`await response.ReadErrorDetailAsync(cancellationToken)`.

The filename comes from `Content-Disposition`, with a local fallback that is a **third** hardcoded
spelling of `ats-audit-trail.xlsx` (the service builds a timestamped name; `AtsAuditExportDTO`'s
default is `"audit-trail.xlsx"`; the two UI fallbacks are `"ats-audit-trail.xlsx"`).

### 6.4 The detail dialog — `AuditTrailDetailDialog.razor` / `.razor.cs`

The shell reuses the generalized gradient header, with the history inline:

```razor
            @* .ats-dialog-headline* are the shared gradient-header rules in ats.css,
               generalized from PreviewComponent when this became their third user. *@
            <header class="ats-dialog-headline ats-dialog-header">
```

`ats.css:1976` confirms it: *".ats-dialog-headline on the `<header>`, and uses the -icon/-text/-close
children."* The rules are at lines 1979-2075.

Deserializing the diff, and the deliberate use of an exception as a branch:

```csharp
	private IReadOnlyList<AuditEntityChangeDTO> EntityChanges
	{
		get
		{
			if (string.IsNullOrWhiteSpace(Entry.Changes))
			{
				return [];
			}

			try
			{
				return JsonSerializer.Deserialize<List<AuditEntityChangeDTO>>(
					Entry.Changes,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
			}
			catch (JsonException)
			{
				// An oversized diff is stored as a marker object, not an array; it fails
				// to deserialize here and falls through to the note below.
				return [];
			}
		}
	}
```

`PropertyNameCaseInsensitive = true` is what bridges the PascalCase JSON the backend writes (§1.5).

The two-way distinction the whole null-vs-empty design exists for:

```csharp
	// Distinguishes "we could not capture this" from "nothing changed", which are very
	// different answers to an auditor.
	private string NoChangesReason =>
		string.IsNullOrWhiteSpace(Entry.Changes)
			? "Field-level changes are not captured for this action type. Actions that write "
				+ "directly to the database - ticketing and email status updates - bypass the "
				+ "change tracker this record is built from."
			: "This action changed too many records to list here. The request itself is shown below.";
```

The second branch is reached only by the `OversizedPayload` marker (§2.9) — `Changes` is non-empty
but does not deserialize to a list.

The masking note, and the correction that had to be added for assistant rows:

```razor
                @* Sensitive values were masked before the entry was stored, so nothing
                   shown here can be un-masked from this screen. *@
                <p class="audit-section-note">
                    Government IDs, birthdates and tokens are stored as
                    <code>@Mask</code> and were never written to this table.
                </p>

                @* The masking above works by PROPERTY NAME, which cannot help with free
                   prose. An assistant transcript is stored exactly as typed, so the note
                   above would be misleading on these rows if left to stand alone. *@
                @if (IsAssistantTranscript)
                {
                    <p class="audit-section-note">
                        This row is a chat transcript. The question and answer are stored
                        exactly as written, so they may contain anything the user typed.
                    </p>
                }
```

with

```csharp
	// Mirrors AtsAuditRedactor.Mask, which lives in the backend assembly. Shown so the
	// reader knows a masked value was never stored rather than merely hidden here.
	private const string Mask = "***";
```

and

```csharp
	// Matched on the action rather than by sniffing the payload's shape: the writer sets
	// this name in AtsAssistantService.RecordAudit, and a JSON probe would misfire on any
	// future command that happens to carry a "Question" field.
	private bool IsAssistantTranscript =>
		string.Equals(Entry.Action, "AskAtsAssistant", StringComparison.OrdinalIgnoreCase);
```

**This UI text is the one place in the whole feature that describes masking correctly** — it says
government IDs and birthdates are stored as `***`, which is what the interceptor actually does. The
design doc, the entity comment and the behaviour's XML doc all say the opposite (**C1**).

`Mask = "***"` is a **fourth** hand-copied constant: `AtsAuditRedactor.Mask` is the source, and
nothing checks the UI copy.

Payload re-indentation, degrading rather than failing:

```csharp
	private string FormattedPayload
	{
		get
		{
			var payload = Entry.Payload ?? string.Empty;

			try
			{
				using var document = JsonDocument.Parse(payload);

				return JsonSerializer.Serialize(
					document.RootElement,
					new JsonSerializerOptions { WriteIndented = true });
			}
			catch (JsonException)
			{
				return payload;
			}
		}
	}
```

State labels translate EF's vocabulary into a human one —
`"Added" → "Created"`, `"Modified" → "Updated"`, `"Deleted" → "Removed"`, anything else passed
through. Those three strings come from `entry.State.ToString()` in the interceptor (§2.8), i.e.
`nameof(EntityState)`, in another assembly. A fifth string coupling.

The clipboard helper swallows a browser refusal on purpose:

```csharp
		catch (JSException)
		{
			// Clipboard access can be refused by the browser; the value is still on
			// screen to select by hand, so this is not worth interrupting the user for.
		}
```

### 6.5 UI service — `Services/ATS/AuditTrail/AuditTrailService.cs`

```csharp
	public AuditTrailService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}
```

`"API"` is the gateway-facing named client, registered
`services.AddScoped<IAuditTrailService, AuditTrailService>();` at
`UI/FrontendWebassembly/ServiceConfig/FrontendServiceConfig.cs:86`. The three URL strings are
relative to the gateway with **no leading slash**, and must match `MatchPath` in §6.6:

| Call | Verbatim |
|---|---|
| page | `var query = $"ats/getaudittrail?pageSize={pageSize}";` then `&cursor=`, `&outcome=`, `&action=`, `&area=`, `&searchTerm=`, `&startDate=`, `&endDate=`, each `Uri.EscapeDataString`-escaped → `_httpClient.GetAsync(query)` |
| counts | `var query = "ats/getauditoutcomecounts";` with a `var separator = '?';` flipped to `'&'` → `_httpClient.GetAsync(query)` |
| export | `var query = "ats/exportaudittrail";` built through a local `void Append(string name, string? value)` helper → `_httpClient.GetAsync(query, cancellationToken)` |

Dates serialize as `"yyyy-MM-dd"` in all three, matching what `BuildRowsQuery` expects (§3.4).

Two different query-building idioms in one file — manual `+=` with a flipped separator for the
first two, a local function for the third. Both are correct; the third is the one to copy.

The two reads deserialize through wrapper records that must match the endpoint response records'
property names (§3.1):

```csharp
// Response envelopes, matching the property names the Carter endpoints return.
public record GetAuditTrailResponseDTO
{
	public KeysetPaginatedResult<AuditTrailListDTO>? AuditEntries { get; set; }
}

public record GetAuditOutcomeCountsResponseDTO
{
	public AuditOutcomeCountsDTO? Counts { get; set; }
}
```

and both treat a null wrapper property as a failure rather than an empty result:

```csharp
			if (result?.AuditEntries is null)
			{
				return ServiceResponse<KeysetPaginatedResult<AuditTrailListDTO>>.Failure(
					"The server returned an empty response.");
			}
```

Export returns the **raw `HttpResponseMessage`**, not bytes:

```csharp
	// Returns the raw response so the caller can read both the bytes and the server-chosen
	// Content-Disposition filename, matching BulkUploadService.ExportSubjectsAsync.
	public async Task<ServiceResponse<HttpResponseMessage>> ExportAuditTrailAsync(
```

which is why the caller can read `ContentDisposition?.FileName`. It also means the response is not
disposed by the service — the component reads it and lets it go out of scope.

Every method follows the same error fold:

```csharp
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<KeysetPaginatedResult<AuditTrailListDTO>>.Failure(
				$"Unable to reach the server. {ex.Message}");
		}
```

Cancellation is rethrown so the MudTable's own cancellation handling works; non-success statuses
surface the server's `detail` via `await response.ReadErrorDetailAsync()`, which is the only path
for the 403 text.

The UI's `AuditTrailListDTO` (`UI/FrontendWebassembly/DTO/ATS/AtsAuditTrailDTO.cs`) is a
field-for-field copy of `ATS.Data.DTO.AuditTrailListDTO` plus three extra records
(`AuditEntityChangeDTO`, `AuditPropertyChangeDTO`, `AuditOutcomeCountsDTO`). **Eighteen properties
duplicated across an assembly boundary with nothing enforcing the match.** A property added to the
backend DTO and not the UI one is silently dropped at deserialization.

### 6.6 Gateway routes — `Path/ATSPaths.cs:448-479`

All three, every one on `GatewayConstants.OnePlatformApi`:

```csharp
			new RouteDefinitionDTO(
				RouteId: "GetAuditTrail",
				MatchPath: "/ats/getaudittrail",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getaudittrail" }
				}
			),
```

and identically for `ExportAuditTrail` (`GET`, `/ats/exportaudittrail` → `/exportaudittrail`) and
`GetAuditOutcomeCounts` (`GET`, `/ats/getauditoutcomecounts` → `/getauditoutcomecounts`).

They sit immediately after the four OMS ticketing routes and, like them, carry **no `RateLimitPolicy`
metadata**, so they fall through to the gateway default. For an admin-only console behind
authentication that is defensible; for `exportaudittrail`, which builds a 10 000-row workbook in
memory per call, it is worth knowing.

Verify all three at runtime with `GET /__routes` on the gateway.

**Three independent string literals per route** must agree: the Carter `MapGet("getaudittrail")`,
the `PathSet` here, and the `"ats/getaudittrail"` in the UI service. `MatchPath` must equal
`"/ats/" + <UI string>`. Nothing checks any of it at compile time.

### 6.7 CSS — mostly shared

`wwwroot/css/ats.css` carries the structure, including two rules specific to this screen that were
put in the *shared* file rather than a scoped one (lines 800-833):
`.ats-management-page .ats-audit-filter-row`, `.ats-audit-filter-row .ats-status-board-segmented`,
`.ats-management-page .ats-audit-export:disabled`, `:disabled:active`, and a
`@media (max-width: 720px)` override for `.ats-audit-export`. The filter row is shared-file because
it wraps the shared segmented control; the export button lives there because, as the markup
explains, *"the export sits beside the chips rather than in the table toolbar: that row's controls
are pinned to a fixed height for alignment"*.

`AuditTrailComponent.razor.css` is **37 lines** and opens with the rule that keeps it that way:

```css
/* Only what is specific to the audit board lives here.

   The intro banner, filter chips, status dots, status pills, lead-identity cell, tag
   and muted cells all come from the shared .ats-status-board-* / .ats-cell-* /
   .ats-status-pill rules in wwwroot/css/ats.css, which Ticketing Status and Bulk
   Uploads Status use too. Do not re-declare any of those here - change the shared
   rules instead so every board stays identical. Success reuses the "done" pill and
   Failure the "error" pill for the same reason. */
```

Its two selectors: `::deep .audit-outcome-cell` (the pill-over-reason stack) and
`::deep .audit-failure-reason` (`-webkit-line-clamp: 2`, `max-width: 260px`,
`color: var(--c-danger-strong)`, with `title="@entry.FailureReason"` in markup supplying the full
text on hover), plus the 720px override relaxing the max-width. This matches the design doc's
claim exactly.

`AuditTrailDetailDialog.razor.css` is **253 lines** — the payload viewer, the change table, the
detail grid, the copy buttons and the code blocks. The design doc's "Scoped CSS is limited to what
is genuinely new" understates it: the dialog is the largest scoped stylesheet in the feature.

---

## 7. Sharp edges

### 7.1 Graceful shutdown does not reliably drain the channel (**C7**)

`AtsAuditDrainService.StopAsync`:

```csharp
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		// Closing the queue lets WaitToReadAsync return false once the backlog is drained,
		// so entries already recorded are not lost on a graceful shutdown.
		_auditWriter.Complete();

		await base.StopAsync(cancellationToken);
	}
```

The comment describes an intent the code does not implement. `base.StopAsync` on a
`BackgroundService` **cancels the stopping token first**, then awaits `ExecuteAsync`. The loop is
suspended on `await reader.WaitToReadAsync(stoppingToken)`; that task faults with
`OperationCanceledException`, and the loop's own catch swallows it:

```csharp
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// Shutting down.
		}
```

There is **no `while (reader.TryRead(out var entry))` pass after `Complete()`**, and no
`WaitToReadAsync(CancellationToken.None)` that would let the completed channel report its remaining
items. `Complete()` and the cancellation race; whichever lands first decides whether the pending
`WaitToReadAsync` returns a value or throws. Entries buffered at shutdown are therefore lost
non-deterministically, and a batch already inside `SaveChangesAsync` is lost too — `WriteBatchAsync`
rethrows `OperationCanceledException` and the outer catch eats it.

`TryComplete()` also returns `false` if the writer was already completed, and the return value is
discarded, so a second `StopAsync` is silent.

If you need a reliable drain, the shape is: `Complete()`, then loop on
`reader.WaitToReadAsync(CancellationToken.None)` until it returns `false`, *then* call
`base.StopAsync` — or register the flush as an `IHostApplicationLifetime.ApplicationStopping`
handler that runs before hosted-service shutdown.

**What is definitely lost:** a crash, `kill -9`, or a container OOM loses everything in the
channel, up to `BufferSize` entries. There is no write-ahead log and no outbox; the queue is
process memory. That is the accepted trade for taking the write off the request thread, but it
means the trail's completeness guarantee is "every action whose entry reached the database", not
"every action".

### 7.2 The diff IS masked, and four places say it is not (**C1**)

Covered in §2.8. The practical consequences:

- The design doc's §6 known-gap "**`Changes` stores unredacted values** … the audit table carries a
  30-day history of old government IDs and birthdates. If retention or access ever loosens, revisit
  this first" is describing a risk that the code already mitigates for the named fields. The real
  residual risk is the *unlisted* columns — mobile numbers, addresses, email, names, free-text
  descriptions — none of which are in `SensitivePropertyNames`.
- An auditor asking "what was the old SSS value" will find `*** → ***` and the dialog's note that
  the value "was never written to this table". That is true, and it is the *interceptor* that makes
  it true — not the redactor, and not the behaviour.
- If you want real before/after values for a sensitive column, removing the name from
  `SensitivePropertyNames` changes **both** the payload and the diff, because they share one set by
  design (`IsSensitiveProperty`). There is no way to unmask the diff without unmasking the payload.

### 7.3 Exporting the audit trail leaves no audit trail

`ExportAuditTrailQueryRequest : IQuery<…>`, and `AtsAuditBehavior` is constrained to
`ICommand<>`. So a bulk download of up to 10 000 rows — containing unmasked before/after values for
every non-listed column, plus IPs and trace ids — is not recorded anywhere. The design doc argues
the opposite principle for report downloads: *"In a background-screening system, who pulled which
report is exactly the access worth a trail."*

`AtsAuditService` already has everything needed to record it (`ICurrentUser`, `IAtsAuditWriter` is
injectable), and the assistant's own audit rows prove a service can write an entry directly
(§2.13). The gap is not architectural, just unimplemented. The only existing evidence of an export
is the ASP.NET request log via `LoggingBehavior`, which *does* see queries.

### 7.4 `AtsAudit:Enabled = false` still fills the channel

`AtsAuditBehavior.Handle` checks `_options.Enabled` before recording, and both hosted services
return early when it is false. `AtsAssistantService.RecordAudit` checks **nothing** — the service
does not even inject `IOptions<AtsAuditOptions>` (its constructor takes fourteen dependencies and
that is not one of them).

So with the feature disabled: every assistant turn enqueues into a bounded channel that nobody
reads. It fills to `Math.Max(100, BufferSize)` and from then on every turn logs
`"The ATS audit queue is full; the entry for {Action} by {UserId} was dropped"` at Warning, forever,
while holding up to 10 000 `AtsAuditEntry` objects (each with an 8 KB payload) alive in a singleton.

`AtsAuditBehaviorTests.Handle_ShouldRecordNothing_WhenAuditingIsDisabled` covers the behaviour and
cannot see this. If `Enabled` is ever used as a kill switch, add the same check to `RecordAudit`.

### 7.5 Sync-over-async in `SavingChanges`

```csharp
		CaptureAsync(eventData.Context, CancellationToken.None)
			.GetAwaiter()
			.GetResult();
```

`CaptureAsync` can await `entry.GetDatabaseValuesAsync(...)` — a real database round trip. On the
synchronous `SaveChanges` overload this blocks a thread-pool thread on an async I/O completion, with
`CancellationToken.None` so it cannot be cancelled.

Verified: **nothing in `BackendAPI` calls the synchronous `SaveChanges()` today** — a grep for
`.SaveChanges()` across the whole backend returns zero hits, every call site is `SaveChangesAsync`.
So the override is dead code in production right now. It is still implemented, and it is the branch
EF would take if any future caller — or an EF internal path, or a test — used the sync overload.
Deadlock risk is low in ASP.NET Core (there is no synchronization context) but thread starvation
under load is not zero, and the cancellation token cannot reach the extra `SELECT`.

If you would rather not carry it, throwing `NotSupportedException` from the sync override would make
the constraint explicit instead of latent.

### 7.6 The interceptor is on the context, not on the audit path

`options.AddInterceptors(...)` in `AddDbContext` (§8) attaches `AtsAuditChangeInterceptor` to
**every** `ATSDBContext`, in every scope: HTTP requests, Quartz jobs, `BulkSubmissionProcessorService`'s
per-file scopes, the drain's own per-batch scope, the retention sweeper's, and integration tests that
build a context by hand.

Consequences:

- Every `SaveChanges` anywhere in ATS pays the change-tracker walk and the `List<AtsEntityChangeDTO>`
  allocation, whether or not a MediatR command is in flight and whether or not anything will ever
  read the result. A Quartz job that saves 500 detached entities pays 500 `GetDatabaseValuesAsync`
  round trips and produces 50 entries that are discarded when the scope ends.
- The collector is scoped and **only** read by `AtsAuditBehavior`. In a background job nothing reads
  it, so the work is pure overhead. There is no "am I inside an audited request?" flag.
- The drain's own `SaveChanges` re-enters the interceptor, guarded only by
  `if (entry.Entity is AtsAuditEntry) continue;` (§2.6). If the audit table is ever written through
  a different entity type or a raw `ExecuteInsert`, that guard stops applying.

### 7.7 The collector accumulates for the whole scope and is never cleared

`AtsAuditChangeCollector` has no `Clear()`. `Add` appends. Within one HTTP request, the collector
holds every change from every `SaveChanges` on every `ATSDBContext` resolved in that scope.

That is correct for the normal case — one command, one or two saves, one read after `next()`. It
breaks in two shapes:

- **Nested dispatch.** If a handler calls `ISender.Send` for a second ATS command, the inner
  `AtsAuditBehavior` reads the collector after the inner handler and records *its* changes; then the
  outer behaviour reads the same collector, which still holds the inner changes, and records them
  **again** on the outer entry. There is no such nesting in ATS today; adding one produces
  duplicated diffs, not an error.
- **Concurrent saves.** `List<T>` is not thread-safe (§2.9). Two interceptor callbacks calling `Add`
  at once can corrupt the list or throw inside `SaveChanges`, which the interceptor does not catch —
  so it would fail the save. `BulkSubmissionProcessorService` avoids this by giving each file its
  own scope; a future fan-out over a shared context would not.

### 7.8 The migration's `descending: new bool[0]` (**C11**)

Three files describe one index and do not visibly agree:

| File | What it says |
|---|---|
| `AtsAuditEntryConfiguration.cs:63` | `.IsDescending(true, true)` |
| `ATSDBContextModelSnapshot.cs:428-429` | `b.HasIndex("OccurredAt", "AuditEntryId").IsDescending();` |
| `20260907160321_AddAtsAuditTrailATSMigration.cs:52` | `descending: new bool[0]` |

The empty array is EF Core's encoding of "all columns descending" — `PlatformLogEventConfiguration.cs:26`
uses `.IsDescending()` with no arguments and its migration
(`PlatformLogging/20260812085040_InitialPlatformLogging.cs:62`) emits the identical
`descending: new bool[0]`, while a *mixed* index in the same file emits
`descending: new[] { false, true }`. So the form is consistent with all-descending, not with
ascending.

Two things still make this worth a second look. First, **the migration file does not prove it** — a
reader auditing index direction from the migration alone cannot tell, and the design doc asserts
`DESC` from the configuration rather than from the database. Second, **the snapshot is what EF diffs
against**, so if the physical index ever turned out to be ascending, no future `dotnet ef migrations
add` would generate a fix. Confirm once with
`psql -c '\d ats."AuditTrail"'` and look for `OccurredAt DESC, "AuditEntryId" DESC`.

Either way the query is correct: `ApplyOrder` (§3.4) is `ORDER BY "OccurredAt" DESC, "AuditEntryId" DESC`
and Postgres can scan a b-tree backwards, so an ascending index costs a `Backward Index Scan` rather
than a sort. The comment's promise — "the keyset page is an index scan rather than a sort" — holds
in both cases.

### 7.9 The site-resolution tests re-implement the query they claim to test

`AtsAuditRepositoryIntegrationTests.SiteResolution_ShouldFindOneSitePerUser_AcrossTheirModuleGrants`
(line 495) does not call `AtsAuditDrainService.ResolveSitesAsync` — it cannot, it is `private static`.
Instead it pastes the same LINQ:

```csharp
		var sitesByUser = await _dbContext.UserDetails
			.AsNoTracking()
			.Where(user => userIds.Contains(user.UserId))
			.GroupBy(user => user.UserId)
			.Select(group => new
			{
				UserId = group.Key,
				Site = group.Select(user => user.Site).FirstOrDefault()
			})
			.ToDictionaryAsync(row => row.UserId, row => row.Site);
```

What it genuinely proves is that **Npgsql can translate that `GroupBy` + `FirstOrDefault` shape** —
which is not trivial and is worth having. What it does not prove is that the drain still uses that
shape. Change `ResolveSitesAsync` to a join, or drop the `GroupBy`, and both site tests keep passing.

The same is true of the four interceptor tests, but in the other direction: they construct
`new AtsAuditChangeInterceptor(collector)` directly and attach it to a hand-built
`DbContextOptionsBuilder`, so they exercise the real class — but not the real DI wiring. A
mis-registered interceptor (§8) would not be caught.

### 7.10 The chat export drops the search term

§5.3. `searchTerm: null` in `AIAssistantComponent.ExportAuditEntriesAsync`, against a DTO that
carries `SearchTerm` and a doc comment promising the export "can never silently widen what the user
was shown". The board's own export (§6.3) passes `_searchString` correctly, so the two export
buttons behave differently for the same underlying filters.

### 7.11 Two producers, one table, no shared builder

`AtsAuditBehavior.Record` (§2.3) and `AtsAssistantService.RecordAudit` (§2.13) each construct an
`AtsAuditEntry` from scratch. They agree today on: `Guid.CreateVersion7()`, `DateTime.UtcNow`,
`(int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)`, and the truncation lengths 500 / 255 /
255 / 64 / 64. They **disagree** on: `Area` (`ResolveArea()` vs the literal `"AIAssistant"`), payload
cap (8 000 enforced vs none), and the `Enabled` check (present vs absent, §7.4).

There is no `AtsAuditEntryFactory`. If you add a column, both initializers need it, and the compiler
will not tell you — `AtsAuditEntry` has all-settable properties and no required members, so a missed
one is a silent default.

### 7.12 The `Action`/`Area` strings are a distributed vocabulary

Nothing enumerates them. `"AskAtsAssistant"` appears as a literal in
`AtsAssistantService.RecordAudit` (the producer), `AtsAuditWorkbookWriter.AssistantAction`, and
`AuditTrailDetailDialog.IsAssistantTranscript` — three files, two assemblies, no shared constant.
`"Web"` and `"PublicApi"` are produced only by string-splitting a namespace. `"Success"`/`"Failure"`
exist as `AuditOutcome` on the backend and as a separately-named `AuditActionOutcome` in the UI.
`"Added"`/`"Modified"`/`"Deleted"` come from `nameof(EntityState)` and are translated to
`"Created"`/`"Updated"`/`"Removed"` by a switch in the dialog.

Rename any of them and the failure is silent: an export column goes blank, a transcript renders as
raw JSON, a filter chip returns zero rows.

---

## 8. Wiring — what is registered where

### 8.1 `BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs`

MediatR, in `AddATSMediaTR` (lines 26-34) — quoted in §2.1. The audit behaviour is registered
**third**, after `ValidationBehavior` and `LoggingBehavior`.

Services, in `AddATSServices`:

```csharp
		// Also uncached: the audit trail is append-only and the screen exists to show what
		// just happened, so a cached first page would hide the newest action.
		services.AddScoped<IAtsAuditRepository, AtsAuditRepository>();
		services.AddScoped<IAtsAuditService, AtsAuditService>();

		// Singleton: the queue has to outlive the request scope that writes to it. The
		// concrete type is registered as well so the drain can read the channel - see the
		// note on AtsAuditDrainService's constructor.
		services.AddSingleton<AtsAuditWriter>();
		services.AddSingleton<IAtsAuditWriter>(provider => provider.GetRequiredService<AtsAuditWriter>());
		services.AddHostedService<AtsAuditDrainService>();
		services.AddHostedService<AtsAuditRetentionService>();
```

Two registrations for the writer, and both are needed. `AddSingleton<AtsAuditWriter>()` is what the
drain resolves (it needs `Reader` and `Complete`, neither of which is on the interface); the
forwarding registration is what request-scoped code resolves. **Registering only the interface would
break the drain at startup; registering only the concrete type would break every `IAtsAuditWriter`
consumer.** Do not "simplify" this to one line.

`IAtsAuditRepository` is registered **directly**, bypassing the `ATSCacheRepository` decorator that
every other ATS repository forwards through:

```csharp
		services.AddScoped<IATSRepository, ATSRepository>();
		services.Decorate<IATSRepository, ATSCacheRepository>();
		…
		services.AddScoped<IReportRepository>(provider => provider.GetRequiredService<IATSRepository>());
```

Caching an append-only trail would hide the newest action, which is the only thing the screen is
for. Same reasoning as `IOMSTicketingRepository` and `IBulkUploadDashboardRepository`, both
registered directly beside it.

Options and the DbContext, in `AddATSInfrastructure`:

```csharp
		// Every AtsAuditOptions value has a working default, so an absent section is
		// valid: the audit trail runs with the agreed 30-day retention out of the box.
		services.Configure<AtsAuditOptions>(
			configuration.GetSection(AtsAuditOptions.SectionName));
```

```csharp
		// The audit change collector and its interceptor are scoped, so the context is
		// built from the request's provider rather than a static lambda.
		services.AddScoped<IAtsAuditChangeCollector, AtsAuditChangeCollector>();
		services.AddScoped<AtsAuditChangeInterceptor>();

		services.AddDbContext<ATSDBContext>((serviceProvider, options) =>
		{
			options.UseNpgsql(
				configuration.GetConnectionString(connStringSegment),
				npgsqlOptions => npgsqlOptions.MigrationsAssembly(assemblyName)
			);

			// Captures before/after values for the audit trail. Reads the change tracker
			// only - it never writes, and a command that saves nothing costs nothing.
			options.AddInterceptors(serviceProvider.GetRequiredService<AtsAuditChangeInterceptor>());
		});
```

The `(serviceProvider, options)` overload is the load-bearing detail: the interceptor needs a
**scoped** `IAtsAuditChangeCollector`, and a scoped service can only be resolved from the scope's
provider. A parameterless `AddDbContext` lambda has no provider and would force the interceptor —
and therefore the collector — to be a singleton, which would make the collector a process-wide
accumulator shared across every concurrent request. That is the failure this overload prevents, and
it is invisible from the interceptor's own file.

`AtsAuditChangeInterceptor` is registered as its **concrete type**, not behind an interface: it is
resolved by `GetRequiredService<AtsAuditChangeInterceptor>()` and never injected anywhere else.

Note the last clause of that comment — *"a command that saves nothing costs nothing"* — is only true
of the change-tracker walk. `CaptureAsync` still runs, allocates a `List<>`, and iterates; see §7.6.

### 8.2 Composition root

`AddATSMediaTR`, `AddATSServices` and `AddATSInfrastructure` are all called from the API
composition root, and `AtsAuditBehavior` needs `ICurrentUser` and `IHttpContextAccessor` from
Auth/ASP.NET. That is why the behaviour lives in the ATS module rather than BuildingBlocks —
BuildingBlocks references neither. `ATS.csproj` references `BuildingBlocks`, `Auth` and `OMS`.

### 8.3 Frontend

`UI/FrontendWebassembly/ServiceConfig/FrontendServiceConfig.cs:86`:

```csharp
		services.AddScoped<IAuditTrailService, AuditTrailService>();
```

one line below the OMS ticketing registration, using the same `"API"` named HttpClient.

The page itself needs no route registration beyond `@page "/s&i/ats/audittrail"` — but see §6.1 for
the four places that must agree before it is reachable.

### 8.4 Tests

| File | Methods | Notes |
|---|---|---|
| `ATS.UnitTests/AtsAuditRedactorTests.cs` | 9 | **C8** — the doc says 11 |
| `ATS.UnitTests/AtsAuditBehaviorTests.cs` | 14 | matches the doc |
| `ATS.UnitTests/AtsAuditBehaviorTestCommands.cs` | — | fakes in `ATS.Features.ThingManagement.Command.AddThing` and `Auth.Features.Login.Command.LoginWeb` |
| `ATS.UnitTests/AtsAuditServiceTests.cs` | 13 | includes the 3 `GetRecentEntriesAsync` and 2 `ExportAuditTrailAsync` tests the doc does not mention |
| `ATS.UnitTests/AtsAuditWorkbookWriterTests.cs` | 8 | **not mentioned in the design doc at all** |
| `ATS.IntegrationTests/AtsAuditRepositoryIntegrationTests.cs` | 20 | includes the 4 `ChangeInterceptor_*` tests and 2 `SiteResolution_*` tests (doc counts them separately) |

`BaseIntegrationTest` truncates the table between tests
(`Test/Test/BackendAPI/Infrastructure/ATS.Infrastracture/BaseIntegrationTest.cs:130`):

```csharp
								ats.""AuditTrail"",
```

inside a `TRUNCATE TABLE … RESTART IDENTITY CASCADE`. Without it, entries leak across runs and the
keyset-walk tests become order-dependent.

Run them:

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~AtsAudit"
dotnet build 1CibiPlatform.sln
```

The integration tests need Docker (Postgres Testcontainer).

---

## 9. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| A `AuditOutcome` constant (`ATS/Constants/AuditOutcome.cs`) | `AuditActionOutcome` in `UI/FrontendWebassembly/DTO/ATS/AtsAuditTrailDTO.cs` | Hand-synced duplicate across an assembly boundary; nothing enforces it (§1.4) |
| Any `HasMaxLength` in `AtsAuditEntryConfiguration.cs` | The matching `Truncate(…, n)` in `AtsAuditBehavior.Record` **and** `AtsAssistantService.RecordAudit` | Three hand-copied numbers; too-long text throws inside the drain's `SaveChangesAsync` and loses the whole batch (§2.3, §2.11) |
| `AtsAuditRedactor.SensitivePropertyNames` | Nothing else — that is the point | One set drives both the payload and the diff via `IsSensitiveProperty` (§2.5, §2.8). Adding a name masks it in both; removing one exposes it in both |
| `AtsAuditRedactor.Mask` | `AuditTrailDetailDialog.Mask` (UI) | Fourth hand-copied `"***"`; the dialog's note would lie (§6.4) |
| `AtsAuditRedactor.MaxPayloadCharacters` | `AtsAuditBehavior.SerializeChanges` and the UI's `NoChangesReason` second branch | The diff cap and the payload cap are the same constant, and the oversized marker is shared (§2.9) |
| `ApplyOrder` in `AtsAuditRepository.cs` | `ApplySeek`, immediately below it | The seek predicate must be the exact row-value comparison of the ordering; a mismatch repeats or skips rows (§3.4) |
| The cursor field count (`CursorCodec.Decode(cursor, 2)`) | `CursorCodec.Encode(...)` in the same method | Two fields, `OccurredAt` then `AuditEntryId`; a mismatch decodes to null and silently restarts at page one (§3.3) |
| A `Features/**` folder name or the `Web`/`PublicApi` split | `AtsAuditBehavior.ResolveArea` | `Area` is the segment after `Features`, resolved at runtime from a namespace string; the unit test's fake does not have a `Web` segment so it will not catch a change (§2.4) |
| A command record's name | Its historical `Action` rows | `Action` is derived by trimming suffixes; renaming splits the trail across two values with no migration (§2.4) |
| `AtsAuditBehavior.Record`'s entry initializer | `AtsAssistantService.RecordAudit` | Two independent producers of one table, no shared factory; both must set every column (§7.11) |
| `"AskAtsAssistant"` anywhere | `AtsAuditWorkbookWriter.AssistantAction`, `AuditTrailDetailDialog.IsAssistantTranscript`, `AtsAssistantService.RecordAudit` | Three literals in two assemblies; a mismatch renders transcripts as raw JSON in both the export and the dialog (§2.13, §7.12) |
| `AtsChatAuditPayloadDTO`'s property names | `AtsAuditWorkbookWriter.ReadString(root, "Question"/"Answer")` and the `"WasRefused"` probe | Matched by JSON string, not by type; a rename silently produces `Q: / A: ` (§4.3) |
| `AtsAuditEntryConfiguration`'s `IsDescending` | The migration **and** the live database | The migration emits `descending: new bool[0]` and the snapshot is what EF diffs against, so drift is never repaired (§7.8) |
| `AtsAuditOptions.BufferSize` | The `BufferSize` comment in the same file | It says the oldest write is dropped; `DropWrite` drops the **newest** (§2.10, §2.15) |
| `AtsAuditOptions.RetentionDays` | `AuditTrailComponent.RetentionDays` (UI) | The screen promises the number in its empty-state copy (§2.14, §6.2) |
| `AtsAuditDrainService.StopAsync` or the loop's cancellation catch | §7.1 | There is no post-`Complete()` drain pass today; entries buffered at shutdown are lost non-deterministically |
| `AtsAuditOptions.Enabled` handling | `AtsAssistantService.RecordAudit` | It does not check the flag; disabling the feature leaves the assistant filling a channel nobody drains (§7.4) |
| `AddDbContext`'s `(serviceProvider, options)` overload | `IAtsAuditChangeCollector`'s scoped lifetime | A parameterless overload would force the collector to singleton and make it a process-wide accumulator shared across requests (§8.1) |
| `options.AddInterceptors(...)` | Every other `ATSDBContext` consumer, including Quartz jobs and the drain | The interceptor is on the context, not on the audit path; all 31 `ExecuteUpdateAsync` sites are invisible to it and every detached `Update()` pays a `SELECT` (§2.7, §7.6) |
| `AtsAuditChangeCollector` | Any handler that fans out concurrent saves over one context, or dispatches a nested command | `List<T>` with no lock and no `Clear()`; nesting double-records diffs (§7.7) |
| A Carter route string (`MapGet("getaudittrail")`) | `PathSet` in `Path/ATSPaths.cs` **and** the URL literal in `Services/ATS/AuditTrail/AuditTrailService.cs` | Three independent literals in three assemblies (§6.5, §6.6) |
| An endpoint response record's property name (`AuditEntries`, `Counts`) | `GetAuditTrailResponseDTO` / `GetAuditOutcomeCountsResponseDTO` in the UI | JSON binding by name; a mismatch surfaces as "The server returned an empty response.", not a compile error (§6.5) |
| `AuditTrailListDTO` (backend) | `AuditTrailListDTO` (UI) and `AtsAuditRepository.Projection` | Eighteen properties copied by hand across an assembly boundary, plus an `Expression` that lists them a third time (§3.4, §6.5) |
| `AtsEntityChangeDTO` / `AtsPropertyChangeDTO` | `AuditEntityChangeDTO` / `AuditPropertyChangeDTO` in the UI, and the `PropertyNameCaseInsensitive` option | The backend serializes with default (PascalCase) options; case-insensitivity is the only thing bridging it (§1.5, §6.4) |
| `AtsModuleIds.AuditTrail` (15) | `ShareData/ATS/ModuleList.cs:32`, `RestrictedAdministrationModuleIds:13`, `ATSInitialData.cs:482`, and the `@page` route's last segment | Four places must agree for the page to be reachable; 15 is deliberately **not** in `IsPrimaryNavigationModule` and has **no** grant backfill (§6.1) |
| `AtsAuditService.CanRead` | `ExportAuditTrailAsync`'s throw, the two empty-result returns, `AtsAssistantPlugin._isPlatformSuperAdmin`, and `ModuleList.RestrictedAdministrationModuleIds` | One rule, five independent enforcements; reads return empty, export throws 403 (§3.3, §4.2, §5.2) |
| `MaxExportRows` or `MaxAssistantEntries` | `AtsAssistantPlugin.MaxAuditResults`, and the silent truncation in `ExportAuditTrailAsync` | Both caps are silent — no marker in the workbook, no note in the chat answer (§4.2, §5.1) |
| `AtsAuditWorkbookWriter`'s `headers` array | `ColumnCount = 12`, `StyleOutcome`'s range, `ApplyLayout`'s column pins (5 and 10) | Four places assume twelve columns and that Cause is 5 / Details is 10; nothing checks (§4.3) |
| `ResolveAuditPeriod` in `AtsAssistantPlugin` | `ExportAuditEntriesAsync`'s `-(DaysBack - 1)` in `AIAssistantComponent.razor.cs` | The window is derived twice, in two languages' assemblies; and that call passes `searchTerm: null`, dropping a filter the DTO carries (§5.3, §7.10) |
| `BuildRowsQuery`'s filter set | `GetAuditTrailPageAsync`, `CountAuditTrailAsync` and `GetOutcomeCountsAsync` | One builder serves all three; adding a filter without threading it through `GetOutcomeCountsAsync` makes the chips disagree with the table (§3.4) |
| Anything adding a filter to the audit screen | The `signature` join in `LoadAuditEntriesAsync` | A filter missing from the signature continues the old keyset walk (§6.2) |
| `.ats-status-board-*`, `.ats-cell-*`, `.ats-status-pill` or `.ats-dialog-headline*` in `ats.css` | Ticketing Status, Bulk Uploads Status, `PreviewComponent`, `BulkUploadSubjectsDialog` | Shared by design across four screens (§6.7) |
| `AtsAuditEntry`'s shape | The interceptor's `if (entry.Entity is AtsAuditEntry) continue;` guard | The drain writes audit rows through a context that has the audit interceptor attached; the guard is the only thing stopping recursion (§2.6, §7.6) |
