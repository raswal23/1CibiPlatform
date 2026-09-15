# OMS Auto-Ticketing — Code Explanation

Companion to [`oms-auto-ticketing.md`](oms-auto-ticketing.md). That document explains *what* the
feature does and *why* the rules exist. This one exists so a developer can change the
implementation without opening every file cold — it walks the real call chains, names the exact
method at each hop, and quotes the code that carries the correctness.

Read it top to bottom once, then use it as a map: *"I'm changing X, what else touches it?"* is
answered by §9.

> **Read §0 first.** The design doc predates several later changes and is now wrong in nine
> places, one of which is an entire slice it never mentions. Every claim below was verified
> against the code on branch `feature/Update-ReadMe-File`; where the two disagree, this document
> follows the code.

---

## 0. Where the design doc no longer matches the code

| # | `oms-auto-ticketing.md` says | The code actually does |
|---|---|---|
| **C1** | Slices live under `Features/OMSTicketing/` | `Features/**Web**/OMSTicketing/` — ATS splits `Features/` by trust boundary |
| **C2** | Three slices: two queries + `RetryTicket` | **Four.** `Command/RetryTickets` (`PATCH retrytickets`) is a full bulk-retry slice with its own validator, a 500-order cap, and multi-select UI. Undocumented. |
| **C3** | `TurnAroundTimeID` = literal `2` | **Derived** from `payload.RushNormal` by `TryResolveTurnAroundTimeId` — Normal → `1`, Rush → `2`; anything else parks the order |
| **C4** | `PackageDetails` matched **by name** | Joined **by `PackageId`**. Name-matching was deliberately removed — see the comment quoted in §2.5 |
| **C5** | "Out of scope reads as 404, not 403" | Half true. There are **two** guards: caller with no ATS access → `ForbiddenException` (403); per-order out of scope → `NotFoundException` (404) |
| **C6** | `MaxDegreeOfParallelism` and `StaleClaimTimeout` are constants "at the top of `OMSTicketingRepository`" | Both live in `OMSTicketingProcessorService`. The repository holds only `ClaimBatchSize`, `PerClientSliceSize`, `MaxTicketAttempts` |
| **C7** | `InternalServerException` / connectivity → retryable | The catch is generic: `catch (Exception ex) when (ex is not OperationCanceledException)`. Same outcome for `InternalServerException`, but **much broader** — see §7.1 |
| **C8** | `CurrentUser` reads the new claims, "falling back to `GivenName`/`Surname`" | **Precedence is reversed**: `ClaimTypes.GivenName` is tried *first*, the custom `firstName` claim is the fallback |
| **C9** | Ticketing scoped CSS = 45 lines, bulk = 92 | 55 and 95. Also the board has **8** columns, not 7 — there is a leading `Select` checkbox column |

Two behaviours exist in the code that the design doc does not describe at all: the **exhausted-ticket
notification** (§2.8) and the **HybridCache tag revocation** after a successful batch (§2.3).

---

## 1. The data model — ticket state lives on the order

### 1.1 Entity — `BackendAPI/Modules/ATS/Data/Entities/EmailInvitationRequest.cs`

A plain POCO; **this entity carries no mapping attributes at all**, everything is fluent (§1.2).

```csharp
	// OMS auto-ticketing. The order is queued at enrolment and the background job
	// claims it by writing TicketStatus, exactly as the email queue does above.
	// IsTicketed is the terminal flag: false means still claimable, true means a
	// ticket number came back from OMS and the row must never be picked up again.
	public string? TicketStatus { get; set; }
	public bool IsTicketed { get; set; }
	public string? TicketNumber { get; set; }
	public DateTime? TicketDeliveryDate { get; set; }
	public DateTime? TicketClaimedAt { get; set; }
	public int TicketAttempts { get; set; }
	public string? TicketError { get; set; }
```

### 1.2 EF configuration — `Data/EntityConfiguration/EmailInvitationRequestConfiguration.cs`

Note the folder: **`Data/EntityConfiguration/`**, not `Data/Configurations/`. It is picked up by
`modelBuilder.ApplyConfigurationsFromAssembly(typeof(ATSDBContext).Assembly);`
(`Data/Context/ATSDBContext.cs:37`) — nothing is configured inline in the DbContext.

```csharp
		builder.Property(e => e.TicketStatus)
			 .HasMaxLength(50)
			 .IsRequired(false);

		builder.Property(e => e.IsTicketed)
			 .IsRequired(true)
			 .HasDefaultValue(false);

		builder.Property(e => e.TicketAttempts)
			 .IsRequired(true)
			 .HasDefaultValue(0);

		builder.Property(e => e.TicketError)
			 .HasMaxLength(500)
			 .IsRequired(false);

		// Drives the OMS ticketing job's claim query and the stale-claim sweeper, the
		// same role EmailSentStatus plays for the email queue.
		builder.HasIndex(e => e.TicketStatus);
```

`TicketNumber` is `HasMaxLength(100)`, `TicketDeliveryDate`/`TicketClaimedAt` are nullable
`timestamptz`. Exactly **one** index was added — the table is write-hot.

**The nullability asymmetry is load-bearing.** The sibling email column is
`EmailSentStatus` → `.IsRequired(true).HasMaxLength(255)`, while `TicketStatus` is
`.IsRequired(false).HasMaxLength(50)`. That single `IsRequired(false)` is *why* every order created
before this feature has `TicketStatus = NULL` and is therefore neither queued nor shown on the
Ticketing Status screen (design doc, "Not done" item 6). `Data/DataSeed/ATSInitialData.cs:100-127`
constructs seed `EmailInvitationRequest` rows without setting either field, so seeded orders are
also unqueued.

### 1.3 Migration — `BackendAPI/API/APIs/Migrations/ATS/20260826103338_AddOMSTicketingColumnsATSMigration.cs`

Note the path: migrations are namespaced per module under `Migrations/ATS/`, namespace
`APIs.Migrations.ATS`. `Up()` is purely additive — seven `AddColumn` calls plus one index:

```csharp
            migrationBuilder.AddColumn<string>(
                name: "TicketStatus",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_TicketStatus",
                schema: "ats",
                table: "EmailInvitationRequest",
                column: "TicketStatus");
```

`Down()` drops the index, then all seven columns. No destructive change.

**The column name has history.** `20260713052049_AddedColumnsForEmailInvitationRequest.cs` renamed
an earlier `TicketStatus` to `OrderStatus`:

```csharp
            migrationBuilder.RenameColumn(
                name: "TicketStatus",
                schema: "ats",
                table: "EmailInvitationRequest",
                newName: "OrderStatus");
```

That original column (added by `20260710065948_AddedTicketStatusForEmailInvitationRequest.cs`) was
`varchar(255) NOT NULL DEFAULT ''` — a **different type and nullability** from today's
`varchar(50) NULL`. So the name is free, but anyone reading old migrations will find a
`TicketStatus` that meant something else entirely. Today's `TicketStatus` is ticketing state;
today's `OrderStatus` is the order lifecycle. They are unrelated.

### 1.4 Vocabulary — `BackendAPI/Modules/ATS/Constants/TicketStatus.cs`

The whole file:

```csharp
namespace ATS.Constants;

/// <summary>
/// Lifecycle of an order's OMS ticket. Public, unlike <see cref="EmailStatus"/>,
/// because the ticketing status screen filters on this vocabulary.
/// </summary>
public static class TicketStatus
{
	public const string Pending = "Pending";

	public const string Processing = "Processing";

	public const string Done = "Done";

	// Terminal until a human intervenes: either OMS rejected the request on business
	// grounds, or the order could not be projected onto the OMS payload at all.
	public const string Error = "Error";

	// The full vocabulary, used to validate a caller-supplied status filter.
	public static readonly string[] All = [Pending, Processing, Done, Error];
}
```

A `public static class` of `public const string` — **not an enum**. `All` exists so the query
validator can check a caller-supplied filter (§3.2). The contrast with `EmailStatus`
(`internal static class`, no `All`) is deliberate: `TicketStatus` crosses the API boundary as a
filter value, `EmailStatus` never does.

---

## 2. The write side — how an order gets queued, and how the job drains it

### 2.1 Three enrolment paths set `Pending`, not two

The design doc names two. There are **three** callers of
`EndorsementSubmissionService.InsertEmailInvitationRequestAsync`:

| # | Caller | File | `source` |
|---|---|---|---|
| 1 | `InsertEmailInvitationRequestHandler.Handle` (web console) | `Features/Web/InsertEmailInvitationRequest/InsertEmailInvitationRequestHandler.cs:51` | defaults to `OrderHistorySource.Web` |
| 2 | `CreateEndorsementHandler.Handle` (**public API**) | `Features/PublicApi/CreateEndorsement/CreateEndorsementHandler.cs:88-91` | `OrderHistorySource.PublicApi` |
| 3 | `AtsAssistantService.ConfirmOrderDraftAsync` (AI assistant) | `Services/AIAssistant/AtsAssistantService.cs:355` | defaults to `Web` |

The shared method (`Services/EndorsementSubmission/EndorsementSubmissionService.cs:85`):

```csharp
	public async Task<bool> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default, string source = OrderHistorySource.Web)
```

and the two lines that queue the order (lines 140-148):

```csharp
		emailInvitationRequest.OrderStatus = OrderStatus.PendingCandidateInfo;

		// Queues the order for OMS auto-ticketing. The background job claims it from
		// here; there is no outbox, the status column is the queue.
		emailInvitationRequest.TicketStatus = TicketStatus.Pending;
		emailInvitationRequest.IsTicketed = false;
		emailInvitationRequest.RequestorId = _currentUser.UserId;
```

The entity is built by `emailInvitationRequestDTO.Adapt<EmailInvitationRequest>()` with
`EmailInvitationID = Guid.CreateVersion7()`, then persisted inside
`TransactionRunner.RunAsync(_unitOfWork, async () => { await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest); ... })`
— so the ticket columns commit **in the same transaction** as the order and its invitation email.
There is no window where an order exists unqueued.

The browser entry point is `Component/ATS/Orders/NewOrderComponent.razor.cs:242` →
`UI/FrontendWebassembly/Services/ATS/EndorsementSubmission/EndorsementSubmissionService.cs:122`.
**That UI class has the same name as the backend service and is a different thing entirely** — an
HttpClient wrapper. Grepping for `EndorsementSubmissionService` returns both.

### 2.2 The bulk path — `Services/BulkSubmissionProcessor/BulkSubmissionProcessorService.cs`

`ProcessAsync` (line 53) creates each row inside a `pendingFiles.Select(async file => ...)` lambda
under a `SemaphoreSlim(3)` with a per-file `IServiceScope`. The object initializer (lines 217-223):

```csharp
						OrderCreatedAt = DateTime.UtcNow,

						// Bulk orders are auto-ticketed on the same terms as single
						// enrolments; the ticketing job claims them from this status.
						TicketStatus = TicketStatus.Pending,
						IsTicketed = false
					});
```

persisted at line 225 via `AddBulkEmailInvitationRequestAsync(subjects)`.

This runs on a **Quartz thread with no `HttpContext`**, so `ClientId`, `RequestorId` and `Requestor`
come from the `BulkUploadFileDetails` row (`file.ClientId`, `file.UploadedByUserId`,
`file.Requestor`) — never from `ICurrentUser`, which would resolve null.

**These are the only two write-side assignments in the repository.** A grep for `TicketStatus =` /
`IsTicketed =` across `BackendAPI` finds these two plus comparisons inside
`OMSTicketingRepository.cs` (lines 79, 227, 262, 296, 487, 523). Notably
`ResendApplicationFormAsync` / `ResendApplicationFormsAsync` do **not** touch ticket state: a
resent order keeps whatever ticket status it already had.

### 2.3 The Quartz tick — `BackgroundJobs/OMSTicketing/`

Two files. The job is a thin shell:

```csharp
namespace ATS.BackgroundJobs.OMSTicketing;

[DisallowConcurrentExecution]
public class OMSTicketingBackgroundJob : IJob
{
	public async Task Execute(IJobExecutionContext context)
	{
		using var loggingScope = _logger.BeginScope(new Dictionary<string, object> { ["Application"] = "ATS" });
		using var scope = _scopeFactory.CreateScope();

		var processor = scope.ServiceProvider
			.GetRequiredService<IOMSTicketingProcessorService>();

		await processor.ProcessAsync(context.CancellationToken);
	}
}
```

`[DisallowConcurrentExecution]` guards within a node; `SKIP LOCKED` (§2.4) guards across nodes —
Quartz uses a **clustered** Postgres store, so both are needed.

The trigger, in `OMSTicketingBackgroundJobSetup.cs`:

```csharp
public class OMSTicketingBackgroundJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(OMSTicketingBackgroundJob));
		options.AddJob<OMSTicketingBackgroundJob>(opts => opts.WithIdentity(jobKey));

		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("OMSTicketingTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInSeconds(10).RepeatForever()));
	}
}
```

Ten seconds, forever. Registered by `services.ConfigureOptions<OMSTicketingBackgroundJobSetup>();`
(§8) — the `IConfigureOptions<QuartzOptions>` shape is what lets several modules each add a job
without any of them owning the Quartz options object.

### 2.4 `ProcessAsync` — one tick, in order

`Services/OMSTicketing/OMSTicketingProcessorService.cs`. The two constants the design doc
mis-attributes to the repository live here (**C6**):

```csharp
public class OMSTicketingProcessorService : IOMSTicketingProcessorService
{
	private static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(30);

	private const int MaxDegreeOfParallelism = 3;
```

The skeleton:

```csharp
	var released = await _repository.ReleaseStaleTicketClaimsAsync(
		StaleClaimTimeout,
		cancellationToken);

	var claimed = await _repository.ClaimPendingTicketsAsync(cancellationToken);

	if (claimed.Count == 0)
	{
		return;
	}

	var claimedIds = claimed
		.Select(order => order.EmailInvitationID)
		.ToList();

	var payloads = await _repository.GetTicketPayloadsAsync(claimedIds, cancellationToken);

	var missing = claimedIds
		.Except(payloads.Select(payload => payload.EmailInvitationID))
		.ToList();

	if (missing.Count > 0)
	{
		await _repository.MarkTicketFailedAsync(
			missing,
			"The order details required to build the OMS ticket could not be loaded.",
			isRetryable: false,
			cancellationToken);
	}

	using var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);

	var tasks = payloads.Select(payload => ProcessOneAsync(payload, semaphore, cancellationToken));

	var results = await Task.WhenAll(tasks);
```

Five steps, in this order, each with a reason:

1. **Sweep stale claims first.** A worker that died mid-batch left rows in `Processing`; without
   this they would sit there forever.
2. **Claim atomically.** §2.5.
3. **Load payloads, then park anything claimed-but-missing.** The `Except` matters: a row that was
   claimed but produced no payload would otherwise stay in `Processing` until the 30-minute sweeper
   released it, looking stuck for no reason. It is parked immediately *with a reason* instead.
4. **Fan out under `SemaphoreSlim(3)`** — three concurrent OMS calls, because each ticket costs
   three stored-procedure round trips to a remote legacy SQL Server.
5. **`Task.WhenAll`**, then per-order results.

After the batch, if any order succeeded the processor **revokes HybridCache tags** `Report`,
`DisputeOrder` and `WithdrawnApplication`. Not in the design doc: a new ticket changes what those
cached pages show, so they are dropped rather than left to expire.

### 2.5 The claim query — the load-bearing SQL

`Data/Repository/OMSTicketing/OMSTicketingRepository.cs`, verbatim:

```csharp
public async Task<List<EmailInvitationRequest>> ClaimPendingTicketsAsync(
	CancellationToken cancellationToken)
{
	return await _dbContext.EmailInvitationRequests
		.FromSqlRaw(
			"""
			WITH ranked AS (
				SELECT "EmailInvitationID",
					 ROW_NUMBER() OVER (
						 PARTITION BY "ClientId"
						 ORDER BY "OrderCreatedAt") AS rn
				FROM ats."EmailInvitationRequest"
				WHERE "IsTicketed" = false
				 AND ("TicketStatus" = {2}
					OR ("TicketStatus" = {3} AND "TicketAttempts" < {4}))
			)
			UPDATE ats."EmailInvitationRequest" t
			SET "TicketStatus" = {0},
				"TicketClaimedAt" = {1}
			WHERE t."EmailInvitationID" IN (
				SELECT e."EmailInvitationID"
				FROM ats."EmailInvitationRequest" e
				WHERE e."EmailInvitationID" IN (
					SELECT "EmailInvitationID" FROM ranked WHERE rn <= {5})
				ORDER BY e."OrderCreatedAt"
				LIMIT {6}
				FOR UPDATE SKIP LOCKED
			)
			RETURNING t.*;
			""",
			TicketStatus.Processing,
			DateTime.UtcNow,
			TicketStatus.Pending,
			TicketStatus.Error,
			MaxTicketAttempts,
			PerClientSliceSize,
			ClaimBatchSize)
		.AsNoTracking()
		.ToListAsync(cancellationToken);
}
```

Positional binding — the order of the `params object[]` **is** the contract, and it does not match
the order the placeholders appear in the SQL:

| Placeholder | Value | Meaning |
|---|---|---|
| `{0}` | `TicketStatus.Processing` | the status being written |
| `{1}` | `DateTime.UtcNow` | claim timestamp |
| `{2}` | `TicketStatus.Pending` | claimable: never attempted |
| `{3}` | `TicketStatus.Error` | claimable: failed but still under budget |
| `{4}` | `MaxTicketAttempts` (5) | the retry budget |
| `{5}` | `PerClientSliceSize` (30) | fair-share cap per client |
| `{6}` | `ClaimBatchSize` (50) | total batch cap |

Three properties of this query carry the correctness:

- **`FOR UPDATE SKIP LOCKED` sits on the inner sub-SELECT**, so row locks exist only for the
  duration of this one `UPDATE`. This is why no staging table is needed: the *durable* claim is the
  `TicketStatus` write, not the lock. Dashboards, list pages and the projection job are never
  blocked by a running ticketing pass.
- **`ROW_NUMBER() OVER (PARTITION BY "ClientId" ...)` with `rn <= 30`** means one client's 10,000-row
  bulk upload cannot occupy the whole 50-row batch and starve everyone else.
- **`IsTicketed = false` is the outer gate.** `TicketStatus` alone is not sufficient — `IsTicketed`
  is the terminal flag that makes a completed order permanently unclaimable even if its status were
  later rewritten.

The constants:

```csharp
public const int MaxTicketAttempts = 5;

private const int PerClientSliceSize = 30;

private const int ClaimBatchSize = 50;
```

`MaxTicketAttempts` is `public` where the other two are `private`, because the UI prints `5/5`
inside the Error pill (§6.4) — the mirror constant is noted in §6.2.

### 2.6 The stale-claim sweeper

```csharp
public async Task<int> ReleaseStaleTicketClaimsAsync(
	TimeSpan staleAfter,
	CancellationToken cancellationToken)
{
	var cutoff = DateTime.UtcNow.Subtract(staleAfter);

	return await _dbContext.EmailInvitationRequests
		.Where(x => x.TicketStatus == TicketStatus.Processing
				 && !x.IsTicketed
				 && x.TicketClaimedAt != null
				 && x.TicketClaimedAt < cutoff)
		.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.TicketStatus, x => TicketStatus.Pending)
			.SetProperty(x => x.TicketClaimedAt, x => (DateTime?)null),
			cancellationToken);
}
```

`staleAfter` is a **parameter**, not a repository constant — the processor owns the 30-minute value.
That timeout must exceed the worst-case batch duration (50 orders ÷ 3 concurrent × 3 stored-proc
round trips to a remote SQL Server) or the sweeper would steal rows from a live worker and cause
duplicate tickets.

### 2.7 Payload loading — three left joins

```csharp
public async Task<List<TicketablePayloadDTO>> GetTicketPayloadsAsync(
	IReadOnlyCollection<Guid> emailInvitationIds,
	CancellationToken cancellationToken)
{
	var query =
		from invitation in _dbContext.EmailInvitationRequests.AsNoTracking()
		where emailInvitationIds.Contains(invitation.EmailInvitationID)

		from personal in _dbContext.PersonalDetails
			.Where(p => p.EmailInvitationID == invitation.EmailInvitationID)
			.DefaultIfEmpty()

		from package in _dbContext.PackageDetails
			.Where(p => p.PackageId == invitation.PackageId)
			.DefaultIfEmpty()

		from user in _dbContext.UserDetails
			.Where(u => invitation.RequestorId.HasValue && u.UserId == invitation.RequestorId.Value)
			.Take(1)
			.DefaultIfEmpty()

		select new TicketablePayloadDTO { /* ... */ };
```

Each `.DefaultIfEmpty()` is a left join, and **left is deliberate**: a missing row must still come
back so the mapper can park the order *with a reason* rather than the row silently vanishing from
the batch (which is what §2.4 step 3's `Except` then catches).

`UserDetails` needs `.Take(1)` because its primary key is composite `(UserId, ModuleId)` — one row
per module grant, each carrying the same `Site`.

**The package join is by id, not by name (C4).** The design doc says "by name"; the code says
otherwise, with the reason inline:

```csharp
				// Joined on the id, not the name. Matching by name meant renaming a package
				// silently orphaned every order that referenced it - they kept the old
				// string and parked here as an error nobody could explain.
			from package in _dbContext.PackageDetails
				.Where(p => p.PackageId == invitation.PackageId)
				.DefaultIfEmpty()
```

Corroborating evidence this was a later deliberate change: `EmailInvitationRequestConfiguration.cs`
comments "PackageId is what OMS ticketing resolves against, so a rename can no longer orphan an
order", and migration `20260830124343_AddPackageIdToOrdersATSMigration` **postdates** the ticketing
migration `20260826103338`.

The doc's "fragile part" warning is still valid, but it now applies only to `ReportTypeID`, which is
parsed out of the free-text `PackageDescription` (§2.9) — not to locating the package row. A vestige
of the old scheme survives in the mapper's *error messages*, which still interpolate the name:
`$"No active package matches \"{payload.SelectPackage}\", so the OMS report type is unknown."`

### 2.8 Per-order isolation — `ProcessOneAsync`

```csharp
private async Task<(Guid Id, bool Succeeded)> ProcessOneAsync(
	TicketablePayloadDTO payload,
	SemaphoreSlim semaphore,
	CancellationToken cancellationToken)
{
	await semaphore.WaitAsync(cancellationToken);

	// Each order gets its own scope: the repository owns a DbContext, which is not
	// safe to share across the concurrent calls above.
	using var scope = _serviceScopeFactory.CreateScope();

	var repository = scope.ServiceProvider.GetRequiredService<IOMSTicketingRepository>();

	try
	{
		var authQueries = scope.ServiceProvider.GetRequiredService<IAuthQueries>();
		var ticketCreator = scope.ServiceProvider.GetRequiredService<IOMSTicketCreator>();
```

The semaphore is released in `finally`. **The per-order `IServiceScopeFactory` scope is not
optional** — `OMSTicketingRepository` holds a `DbContext`, and a single DbContext used from three
concurrent tasks throws or silently corrupts change tracking.

The requestor is resolved from **persisted data**, not `ICurrentUser` (which is
`IHttpContextAccessor`-backed and resolves null on a Quartz thread): `Site` from `UserDetails` by
`RequestorId`, and first/last name from `IAuthQueries.GetATSAssignedUserAsync` — the sanctioned
cross-module lookup. `EmailInvitationRequest.Requestor` holds only a joined display name, which
cannot be split back apart safely.

Mapping failure parks **before** any OMS call:

```csharp
		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			requestor?.FirstName,
			requestor?.LastName);

		if (request is null)
		{
			await repository.MarkTicketFailedAsync(
				[payload.EmailInvitationID],
				failure!,
				isRetryable: false,
				cancellationToken);

			await NotifyIfTicketingExhaustedAsync(
				scope, repository, payload.EmailInvitationID, cancellationToken);

			return (payload.EmailInvitationID, false);
		}
```

No wasted PO validation against the legacy system for an order that could never be ticketed.

### 2.9 Payload mapping — `Services/OMSTicketing/OMSTicketPayloadMapper.cs`

`public static class`, pure, documented as "Pure and static so the mapping rules can be tested
without a database or a live OMS connection." Failure is signalled by a **tuple**, not an exception
and not a bare null:

```csharp
public static (CreateOMSTicketRequest? Request, string? Failure) TryMap(
	TicketablePayloadDTO payload,
	string? requestorFirstName,
	string? requestorLastName)
```

`(null, "<reason>")` on any un-mappable condition. The class doc states: *"A failure here is never
retryable: none of these inputs change on their own."* That is why §2.8 passes
`isRetryable: false`.

**Phone normalisation** — the subject's own number is preferred, then the order's:
`NormalizePhoneNumber(payload.PersonalMobileNumber) ?? NormalizePhoneNumber(payload.MobileNumber)`.

```csharp
public static string? NormalizePhoneNumber(string? value)
{
	if (string.IsNullOrWhiteSpace(value))
	{
		return null;
	}

	var digits = new string(value.Where(char.IsDigit).ToArray());

	// +63 917... and 63917... both denote the same local 0917... number.
	if (digits.Length == 12 && digits.StartsWith("63", StringComparison.Ordinal))
	{
		digits = string.Concat("0", digits.AsSpan(2));
	}

	// A bare 9-prefixed mobile number is missing only its trunk zero.
	if (digits.Length == 10 && digits.StartsWith('9'))
	{
		digits = "0" + digits;
	}

	// The OMS validator accepts 11 or 12 digits; anything else it would reject.
	return digits.Length is 11 or 12
		? digits
		: null;
}
```

Stripping non-digits first is what makes `+63` collapse to `63`. Returning `null` parks the order
locally rather than letting OMS reject it remotely.

**Government ids** — kept only at exactly the right length, otherwise sent blank (the fields are
optional, so a malformed value must not fail the whole ticket):

```csharp
public static string? NormalizeGovernmentId(string? value, int requiredLength)
{
	if (string.IsNullOrWhiteSpace(value))
	{
		return null;
	}

	var digits = new string(value.Where(char.IsDigit).ToArray());

	return digits.Length == requiredLength
		? digits
		: null;
}
```

Called as `NormalizeGovernmentId(payload.SSS, 10)` and `NormalizeGovernmentId(payload.TIN, 12)`.

**`ReportTypeID`** — the leading digit run of a free-text column:

```csharp
private static bool TryParseReportTypeId(string packageDescription, out int reportTypeId)
{
	reportTypeId = 0;

	var digits = packageDescription.Trim();
	var end = 0;

	while (end < digits.Length && char.IsDigit(digits[end]))
	{
		end++;
	}

	return end > 0
		&& int.TryParse(digits[..end], NumberStyles.None, CultureInfo.InvariantCulture, out reportTypeId)
		&& reportTypeId > 0;
}
```

So `"182"`, `" 182 "` and `"182 - Criminal Records Check"` all yield `182`. Failure parks the order
as `Error` with the reason, and OMS is never called.

**`TurnAroundTimeID` is derived, not the literal `2` the design doc claims (C3):**

```csharp
internal const int NormalTurnAroundTimeId = 1;
internal const int RushTurnAroundTimeId = 2;

public static bool TryResolveTurnAroundTimeId(string? rushNormal, out int turnAroundTimeId)
{
	turnAroundTimeId = OrderType.Normalize(rushNormal) switch
	{
		OrderType.Rush => RushTurnAroundTimeId,
		OrderType.Normal => NormalTurnAroundTimeId,
		_ => 0
	};

	return turnAroundTimeId > 0;
}
```

An unrecognised `RushNormal` returns `false` and parks the order — a new order type added elsewhere
will surface here as a visible `Error` rather than silently going out as Normal.

### 2.10 The OMS call — `IOMSTicketCreator` → stored procedures

`BackendAPI/Modules/OMS/Shared/Contracts/IOMSTicketCreator.cs`:

```csharp
	Task<OMSTicketCreated> CreateTicketAsync(
		CreateOMSTicketRequest request,
		CancellationToken cancellationToken,
		string referenceNumber = "");
```

`referenceNumber` is optional, so the pre-existing OMS caller
(`OMS/Features/Tickets/Command/CreateTicket/CreateTicketHandler.cs:88-95`, which passes two
arguments) is unaffected. The ATS job does pass it:

```csharp
			// The invitation id travels as the OMS reference number so the ticket can
			// be tied back to this order, and so a retry after a timeout is
			// recognisable rather than creating a second ticket.
			var ticket = await ticketCreator.CreateTicketAsync(
				request,
				cancellationToken,
				payload.EmailInvitationID.ToString("D"));
```

`OMSTicketCreator` (sealed, primary constructor on `IOMSRepository` + `ILogger`) runs, in order:
name normalisation → `ValidateRequestorAsync` (throws `BadRequestException("Requestor is invalid")`)
→ `ValidatePONumberAsync` (throws
`BadRequestException("PO is insufficient or invalid, please contact your manager")`) →
`repository.CreateTicketAsync(request, referenceNumber, cancellationToken)`; a `null` result throws
`InternalServerException("Ticket creation failed.")`.

Note the signature asymmetry: `IOMSRepository.CreateTicketAsync` takes `referenceNumber` as a
**required positional** parameter, while the creator's is optional.

`Data/Repository/OMSRepository.cs` calls three stored procedures by name with
`CommandType.StoredProcedure`:

```csharp
	private const string ValidateRequestorProcedure = "[dbo].[validate_requestor_api]";
	private const string ValidatePONumberProcedure = "[dbo].[validate_ponumber_api]";
	private const string CreateTicketProcedure = "[dbo].[create_ticket_api_oms]";
```

The null/empty mapping the design doc relies on:

```csharp
			command.Parameters.Add("@p_birthdate", SqlDbType.DateTime).Value = (object?)request.DateOfBirth ?? DBNull.Value;
			command.Parameters.Add("@p_sss_id_number", SqlDbType.NVarChar).Value = (object?)request.SSSIDNumber ?? string.Empty;
			command.Parameters.Add("@p_tin_id_number", SqlDbType.NVarChar).Value = (object?)request.TIN ?? string.Empty;
```

The `(object?)` cast is required — without it `??` unboxes the `DateTime?` instead of selecting the
`object` overload. Date of birth becomes `DBNull`, the two ids become empty strings. This is what
makes ticketing at enrolment safe: `PersonalDetails` does not exist yet, so `DateOfBirth` is null
and SSS/TIN are blank on the first attempt.

One parameter name is a **deliberate misspelling** that must not be "fixed":

```csharp
			// The legacy stored procedure declares this parameter with the
			// "coutry" misspelling; the name must match it exactly.
			command.Parameters.Add("@p_coutry_id", SqlDbType.Int).Value = request.CountryID;
```

`@p_reference_no` is bound directly with no null-coalesce — safe only because the parameter is a
non-nullable `string` defaulting to `""`.

Read-back:

```csharp
			if (await reader.ReadAsync(cancellationToken))
			{
				return new OMSTicketCreated(
					reader["ticket_no"]?.ToString() ?? string.Empty,
					Convert.ToDateTime(reader["delivery_date"]));
			}

			return null;
```

A `SqlException` is caught, logged, and rethrown as
`InternalServerException("Please verify the Package ID details, TurnAroundTime option, available PO credits, and contract validity date, then resubmit the order once confirmed.")`
— which the processor classifies as **retryable** (§2.11).

`Data/Connection/OMSSqlConnectionFactory.cs` opens a fresh `SqlConnection` per call with **no
pooling configuration and no retry policy**, so one ticket costs three open/close cycles against
the legacy server. See §7.1 for the sharp edge in its guard clause.

### 2.11 Failure classification and the retry budget

Two catch blocks decide everything (**C7** — the second is generic, not `InternalServerException`):

```csharp
	catch (BadRequestException ex)
	{
		// OMS rejected the request on business grounds - an unknown requestor or an
		// exhausted PO. Retrying cannot fix either, so it needs a human.
		await repository.MarkTicketFailedAsync(
			[payload.EmailInvitationID],
			ex.Message,
			isRetryable: false,
			cancellationToken);
		...
	}
	catch (Exception ex) when (ex is not OperationCanceledException)
	{
		// Anything else is treated as transient: the order goes back into the queue
		// and is retried until the attempt cap is reached.
		await repository.MarkTicketFailedAsync(
			[payload.EmailInvitationID],
			ex.Message,
			isRetryable: true,
			cancellationToken);
		...
	}
```

Only `BadRequestException` and `OperationCanceledException` are special-cased. **Everything else is
retryable**, which is broader than the design doc implies — see §7.1.

The budget itself is one line in the repository:

```csharp
public async Task<int> MarkTicketFailedAsync(
	IReadOnlyCollection<Guid> emailInvitationIds,
	string reason,
	bool isRetryable,
	CancellationToken cancellationToken)
{
	var trimmedReason = reason.Length > 500
		? reason[..500]
		: reason;

	return await _dbContext.EmailInvitationRequests
		.Where(x => ids.Contains(x.EmailInvitationID))
		.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.TicketStatus, x => TicketStatus.Error)
			.SetProperty(x => x.TicketClaimedAt, x => (DateTime?)null)
			.SetProperty(x => x.TicketError, x => trimmedReason)

			.SetProperty(
				x => x.TicketAttempts,
				x => isRetryable ? x.TicketAttempts + 1 : MaxTicketAttempts),
			cancellationToken);
}
```

Non-retryable **jumps straight to the cap** rather than incrementing, so a condition that cannot
resolve itself is not re-attempted five times over fifty seconds. The 500-character truncation
matches the `TicketError` column width (§1.2) — without it a long OMS exception message would throw
on write and mask the original failure.

### 2.12 The exhausted-ticket notification (undocumented in the design doc)

Every failure path ends with `NotifyIfTicketingExhaustedAsync(scope, repository, id, ct)`. It asks
`repository.GetExhaustedTicketIdsAsync([id], ...)` and — **only when the id is genuinely exhausted**
— resolves `IAtsNotificationService` from the per-order scope and raises
`AtsNotificationType.TicketingFailed`.

The design doc describes the park as silent. It is not: the requestor is told exactly once, at the
moment the automatic budget runs out, which is also the moment the manual Retry button appears
(§5). Checking exhaustion through the repository rather than inferring it from the attempt count is
what makes it fire once instead of on every subsequent pass.

### 2.13 Writing success back

`MarkTicketedAsync(Guid, string ticketNumber, DateTime deliveryDate, ...)` stamps the DateTimeKind
before writing to the `timestamptz` column:

```csharp
		var deliveryDateUtc = ToUtc(deliveryDate);
```

This is the "Npgsql date-kind bug" the design doc's test section alludes to but never explains.
Npgsql refuses a `DateTime` with `Kind = Unspecified` for a `timestamptz` column, and the value comes
from `Convert.ToDateTime(reader["delivery_date"])` over `Microsoft.Data.SqlClient` — which produces
exactly that. The fix lives on the **write** side, here, not in the reader.

---

## 3. The read side — one request end to end (`GetTicketedOrders`)

Traced in full because it is the most complete slice. The other three are diffs in §4.

```
GET /ats/getticketedorders?pageSize=10&status=Error&searchTerm=...
  → YARP route "GetTicketedOrders"  (Path/ATSPaths.cs, PathSet → /getticketedorders)
    → GetTicketedOrdersEndpoint  (Carter, MapGet "getticketedorders")
      → sender.Send(GetTicketedOrdersQueryRequest)              [MediatR]
        → ValidationBehavior → GetTicketedOrdersQueryRequestValidator
        → LoggingBehavior
        → GetTicketedOrdersHandler.Handle
          → IOMSTicketingMonitoringService.GetTicketedOrdersAsync
            → IAtsAccessScopeResolver.ResolveAsync          [null → empty page, no 403]
            → IOMSTicketingRepository.GetTicketedOrdersPageAsync
            → IOMSTicketingRepository.CountTicketedOrdersAsync
      ← KeysetPaginatedResult<TicketedOrderListDTO>
```

### 3.1 Endpoint — `Features/Web/OMSTicketing/Query/GetTicketedOrders/GetTicketedOrdersEndpoint.cs`

Note **`Web/`** in the path (**C1**):

```csharp
public record GetTicketedOrdersEndpointRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? Status = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);

public record GetTicketedOrdersEndpointResponse(KeysetPaginatedResult<TicketedOrderListDTO> TicketedOrders);
```

```csharp
		app.MapGet("getticketedorders", async (
			[AsParameters] GetTicketedOrdersEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
```

`[AsParameters]` is what binds six query-string values to one record. Metadata chain:
`.WithName("GetTicketedOrders")`, `.WithTags("ATS")`,
`.Produces<GetTicketedOrdersEndpointResponse>(StatusCodes.Status200OK)`,
`.ProducesProblem(StatusCodes.Status400BadRequest)`, `.RequireAuthorization()`.

The response record's property name (`TicketedOrders`) is the JSON wrapper the UI must match when
deserialising (§6.5) — two independently written types with nothing enforcing the agreement.

### 3.2 Query, validator, handler — `GetTicketedOrdersHandler.cs`

All three in one file, per the project convention:

```csharp
public record GetTicketedOrdersQueryRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? Status = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null)
	: IQuery<GetTicketedOrdersQueryResult>;

public record GetTicketedOrdersQueryResult(KeysetPaginatedResult<TicketedOrderListDTO> TicketedOrders);
```

```csharp
public class GetTicketedOrdersQueryRequestValidator : AbstractValidator<GetTicketedOrdersQueryRequest>
{
	public GetTicketedOrdersQueryRequestValidator()
	{
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");

		// Cursor is deliberately unvalidated: cursors are opaque and a malformed one
		// self-heals to the first page rather than failing the request.
		RuleFor(x => x.Status)
			.Must(status => string.IsNullOrWhiteSpace(status)
				|| TicketStatus.All.Contains(status, StringComparer.OrdinalIgnoreCase))
			.WithMessage($"Status must be empty or one of: {string.Join(", ", TicketStatus.All)}.");
	}
}
```

`TicketStatus.All` (§1.4) is why that constant is `public`. The unvalidated cursor is a deliberate
choice: `CursorCodec` produces opaque strings, and a bad one degrades to page one instead of 400-ing
a user who merely had a stale URL.

```csharp
	public async Task<GetTicketedOrdersQueryResult> Handle(
		GetTicketedOrdersQueryRequest request,
		CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm,
			request.StartDate,
			request.EndDate);

		var ticketedOrders = await _ticketingMonitoringService.GetTicketedOrdersAsync(
			paginationRequest,
			request.Status,
			cancellationToken);

		return new GetTicketedOrdersQueryResult(ticketedOrders);
	}
```

The handler folds the query string into the shared `KeysetPaginationRequest` from BuildingBlocks and
does nothing else — all logic is in the monitoring service.

### 3.3 Monitoring service and the scope ladder — `Services/OMSTicketingMonitoring/`

Two files: `IOMSTicketingMonitoringService.cs` and
`OMSTicketingMonitoringService.cs` (`public sealed class`). Constructor takes four dependencies:
`ILogger<OMSTicketingMonitoringService>`, `IOMSTicketingRepository`, `IAtsAccessScopeResolver`,
`IOrderHistoryService`.

Read/write separation is deliberate: this service only reads and retries, while
`OMSTicketingProcessorService` (§2.4) only writes. It mirrors the existing
`BulkUploadMonitoring` vs `BulkSubmissionProcessor` split.

The scope contract:

```csharp
public readonly record struct AtsAccessScope(
	IReadOnlyCollection<int>? AuthorizedClientIds,
	Guid? RequiredOwnerId);

public interface IAtsAccessScopeResolver
{
	/// <summary>
	/// Returns null when the caller may not read ATS records at all. A non-null value
	/// carries the client/owner predicates the query must apply.
	/// </summary>
	Task<AtsAccessScope?> ResolveAsync(CancellationToken cancellationToken);
}
```

On the **read** path a null scope yields an empty page, not a 403:

```csharp
		var scope = await _scopeResolver.ResolveAsync(cancellationToken);

		// A caller outside the role ladder reads an empty list rather than a 403, which
		// is how every other ATS list behaves.
		if (scope is not { } accessScope)
		{
			return new KeysetPaginatedResult<TicketedOrderListDTO>(
				Array.Empty<TicketedOrderListDTO>(),
				null,
				0);
		}
```

`GetStatusCountsAsync` does the same, returning `new TicketStatusCountsDTO()`. Both then thread
`accessScope.AuthorizedClientIds` and `accessScope.RequiredOwnerId` into the repository.

**The write path is different — see §5.** This read-returns-empty / write-throws split is the source
of the design doc's confusion (**C5**).

---

## 4. The other three slices, as diffs from §3

All under `Features/Web/OMSTicketing/`, one `{Endpoint,Handler}.cs` pair per folder.

| Slice | Route | Handler calls | What differs from §3 |
|---|---|---|---|
| **GetTicketStatusCounts** | `GET getticketstatuscounts` | `GetStatusCountsAsync(searchTerm, startDate, endDate, ct)` | **No validator at all** — the only one of the four without one. Request is `(SearchTerm, StartDate, EndDate)`; response is `GetTicketStatusCountsEndpointResponse(TicketStatusCountsDTO Counts)`. Its `.WithDescription` states the rule: *"Honours the search and date filters but never the selected status, so every bucket keeps reporting its own size."* Passing `Status` here would make the active chip's count collapse to its own filtered total. |
| **RetryTicket** | `PATCH retryticket` | `RetryTicketAsync(emailInvitationId, ct)` | Request `RetryTicketEndpointRequest(Guid EmailInvitationId)`. Validator: `.NotEmpty()`. Problems declared: 400, **404, 409**. Returns a **bare bool**, not the record — `return Results.Ok(response.Success);` against `.Produces<bool>(...)`. Full walkthrough in §5. |
| **RetryTickets** (bulk, **C2**) | `PATCH retrytickets` | `RetryTicketsAsync(emailInvitationIds, ct)` | Request `RetryTicketsEndpointRequest(IReadOnlyCollection<Guid> EmailInvitationIds)`; response `(int RequestedCount, int RequeuedCount, bool IsComplete)`. Problems: 400, **403**, 404 — **no 409**, because a stale selection is reported in the counts rather than thrown. |

The bulk validator carries the cap, with the reason for duplicating it inline:

```csharp
public class RetryTicketsCommandValidator : AbstractValidator<RetryTicketsCommand>
{
	public RetryTicketsCommandValidator()
	{
		RuleFor(x => x.EmailInvitationIds)
			.NotNull()
			.WithMessage("At least one order is required.")
			.Must(ids => ids is { Count: > 0 })
			.WithMessage("At least one order is required.");

		// The cap is enforced in the service too, because that is where the reason for it
		// lives. Validating here turns an oversized request into a 400 before it reaches a
		// database round trip.
		RuleFor(x => x.EmailInvitationIds)
			.Must(ids => ids is null || ids.Count <= OMSTicketingMonitoringService.MaxBulkRetrySize)
			.WithMessage(
				$"A bulk retry is limited to {OMSTicketingMonitoringService.MaxBulkRetrySize} orders at a time.");

		RuleForEach(x => x.EmailInvitationIds)
			.NotEmpty()
			.WithMessage("Email Invitation ID is required.");
	}
}
```

The endpoint null-guards the collection before constructing the command:
`new RetryTicketsCommand(request.EmailInvitationIds ?? [])`.

`MaxBulkRetrySize` and its rationale:

```csharp
	// A bulk retry is bounded because each requeued order becomes an OMS round trip on the
	// ticketing job's next passes. Releasing thousands at once would monopolise that job
	// and block every other client behind one operator's click.
	public const int MaxBulkRetrySize = 500;
```

The bulk path also writes history differently — one call for the **eligible** ids only, with no
per-row previous status:

```csharp
		if (eligibleIds.Count > 0)
		{
			await _orderHistoryService.RecordManyAsync(
				eligibleIds,
				OrderHistoryEventType.TicketRetryRequested,
				null,
				string.Empty,
				cancellationToken);
		}
```

---

## 5. Manual retry — where the concurrency guard lives

`OMSTicketingMonitoringService.RetryTicketAsync`, verbatim (lines 134-192):

```csharp
	public async Task<bool> RetryTicketAsync(
		Guid emailInvitationId,
		CancellationToken cancellationToken)
	{
		_logger.LogInformation("Retrying OMS ticketing for an order: {@Context}", logContext);

		// Retry takes a caller-supplied id, so the caller's scope is enforced here
		// rather than trusting the page to only offer ids it already listed.
		if (await _scopeResolver.ResolveAsync(cancellationToken) is not { } accessScope)
		{
			throw new ForbiddenException("The current user does not have ATS access.");
		}

		var target = await _ticketingRepository.GetRetryTargetAsync(emailInvitationId, cancellationToken);

		// Out of scope reads as not found: the response must not reveal that an order
		// the caller may not see exists. Same rule the resend path applies.
		if (target is null || !IsWithinScope(target, accessScope))
		{
			throw new NotFoundException($"Email invitation with ID {emailInvitationId} not found.");
		}

		var requeued = await _ticketingRepository.RequeueExhaustedTicketAsync(
			emailInvitationId,
			cancellationToken);

		// The button was stale: the job re-claimed the order, someone else retried it,
		// or it is not exhausted at all. Say so rather than reporting a silent success.
		if (!requeued)
		{
			throw new ConflictException(
				"This order is no longer awaiting a retry. Refresh the list to see its current status.");
		}

		// Records who forced the retry. The order's own status is unchanged - this is a
		// ticketing action, not a step in the order lifecycle - so it is written on
		// both sides of the entry.
		await _orderHistoryService.RecordAsync(
			emailInvitationId,
			OrderHistoryEventType.TicketRetryRequested,
			target.OrderStatus,
			target.OrderStatus ?? string.Empty,
			cancellationToken);

		return true;
	}
```

**Three distinct outcomes, and conflating them is the bug the design doc's C5 wording invites:**

| Condition | Exception | Status | Why |
|---|---|---|---|
| Caller has no ATS access at all | `ForbiddenException` | **403** | Nothing about any order is disclosed — the caller is simply not an ATS user |
| Order unknown **or** outside the caller's client/owner scope | `NotFoundException` | **404** | A 403 here would confirm that another client's order exists |
| Order no longer in the exhausted state | `ConflictException` | **409** | Stale button: the job re-claimed it, or another operator won the race |

The scope predicate:

```csharp
	// Mirrors the read path's scope rule: a null client set means unrestricted (super
	// admin), and RequiredOwnerId restricts a user to the orders they raised.
	private static bool IsWithinScope(TicketRetryTargetDTO target, AtsAccessScope scope)
	{
		if (scope.AuthorizedClientIds is { } clientIds
			&& (target.ClientId is not { } clientId || !clientIds.Contains(clientId)))
		{
			return false;
		}

		return !scope.RequiredOwnerId.HasValue
			|| target.RequestorId == scope.RequiredOwnerId.Value;
	}
```

### 5.1 The requeue `WHERE` clause is the correctness of this feature

```csharp
public async Task<bool> RequeueExhaustedTicketAsync(
	Guid emailInvitationId,
	CancellationToken cancellationToken)
{
	var updated = await _dbContext.EmailInvitationRequests
		.Where(x => x.EmailInvitationID == emailInvitationId
				 && !x.IsTicketed
				 && x.TicketStatus == TicketStatus.Error
				 && x.TicketAttempts >= MaxTicketAttempts)
		.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.TicketStatus, x => TicketStatus.Pending)
			.SetProperty(x => x.TicketAttempts, x => 0)
			.SetProperty(x => x.TicketError, x => (string?)null)
			.SetProperty(x => x.TicketClaimedAt, x => (DateTime?)null),
			cancellationToken);

	return updated > 0;
}
```

Matching the exhausted state **inside the `UPDATE`** rather than reading it first is what makes this
race-safe: if the job re-claimed the row a moment ago, or a second operator clicked first, the
predicate no longer matches, zero rows update, `false` returns, and the caller gets a 409. A
read-then-write would have both callers succeed and the order queued twice.

`!IsTicketed` is the guard against requeueing an order that already has a ticket number — without it
a retry would raise a **duplicate ticket in OMS**.

Resetting `TicketAttempts` to `0` gives the order a fresh full budget of five automatic attempts.

The bulk sibling `RequeueExhaustedTicketsAsync(IReadOnlyCollection<Guid>, ...)` uses the **same**
predicate and returns the count of rows that actually moved — which is how the UI can report
"N of M queued, the rest were already back in the queue" instead of lying about success.

### 5.2 The audit entry

`Constants/OrderHistoryEventType.cs:14`:

```csharp
	public const string TicketRetryRequested = "TicketRetryRequested";
```

`Services/OrderHistory/OrderHistoryService.cs:16-17` delegates straight to the factory:

```csharp
	public Task RecordAsync(Guid invitationId, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web) =>
		_repository.AddAsync(_factory.Create(invitationId, eventType, previousStatus, newStatus, source), cancellationToken);
```

and `OrderHistoryFactory.Create` is where the acting user is stamped:

```csharp
	public OrderStatusHistory Create(Guid invitationId, string eventType, string? previousStatus, string newStatus, string source = OrderHistorySource.Web, Guid? changedByUserId = null)
	{
		// Background jobs run with no HttpContext, so ICurrentUser resolves to null
		// there. They pass the originating user explicitly instead.
		var userId = changedByUserId ?? _currentUser.UserId;

		return new OrderStatusHistory
		{
			OrderStatusHistoryId = Guid.CreateVersion7(),
			EmailInvitationRequestId = invitationId,
			EventType = eventType,
			PreviousStatus = previousStatus,
			NewStatus = newStatus,
			Source = source,
			OccurredAt = DateTime.UtcNow,
			ChangedByUserId = userId == Guid.Empty ? null : userId
		};
	}
```

`ICurrentUser` resolves normally here because `OMSTicketingMonitoringService` is **HTTP-scoped** —
unlike the Quartz job, which must pass `changedByUserId` explicitly. The retry passes
`target.OrderStatus` as *both* previous and new status: a ticket retry is not a step in the order
lifecycle, so the order's own status must not appear to change.

---

## 6. Frontend — the same round trip from the browser

### 6.1 Page, guards, and how it becomes reachable

`UI/FrontendWebassembly/Component/ATS/OMSTicketing/` holds three files:
`TicketingStatusComponent.razor`, `.razor.cs`, `.razor.css`.

```razor
@page "/s&i/ats/ticketingstatus"
@namespace FrontendWebassembly.Component.ATS
@layout ATSLayout
@attribute [RequirePermission(6, 7)]
@attribute [RequireATSModule(14)]
@inherits CrudPageBase
@inject IOMSTicketingService OMSTicketingService

<PageTitle>ATS - Ticketing Status</PageTitle>
```

Three separate things make the page reachable, and all three must agree:

1. **Sidebar entry** — `Layout/ATSLayout.razor:48-56` iterates `ModuleList.List` filtered by
   `IsPrimaryNavigationModule(item.Key) && _accessibleModuleIds.Contains(item.Key)`, building
   `href="@($"/s&i/ats/{module.Value.path}")"`. Module 14 is primary nav per
   `ShareData/ATS/ModuleList.cs:44-46`:
   ```csharp
 	// Modules that belong in the primary sidebar navigation rather than under Manage.
 	public static bool IsPrimaryNavigationModule(int moduleId) =>
 		moduleId <= 5 || moduleId == 12 || moduleId == 13 || moduleId == 14;
   ```
   Add a module 16 and forget this line, and it renders nowhere.
2. **Route guard** — `ATSLayout.razor:305-308` resolves the first URL segment back to a module id
   and checks `_accessibleModuleIds`.
3. **The component's own short-circuit** — `.razor.cs` `OnInitializedAsync` begins
   `if (!IsPageAuthorized) return;`. The code comments this explicitly: *"Without this guard the
   RequirePermission/RequireATSModule attributes are inert."* The attributes set a flag; they do not
   block rendering by themselves.

Deep links are supported via `[SupplyParameterFromQuery(Name = "search")] SearchFromQuery`, which
pre-fills the search box. Two sibling components now cite this file for that subtlety —
`Component/ATS/BulkUploads/BulkUploadsComponent.razor.cs:35` and
`Component/ATS/Orders/SearchReportComponent.razor.cs:39` both say *"See TicketingStatusComponent for
the full note."*

### 6.2 Status buckets — and a hand-synced duplicate

```csharp
	// null is the "All" segment; the other four are the TicketStatus vocabulary.
	private static readonly StatusSegment[] StatusSegments =
	[
		new StatusSegment(null, "All", "is-all"),
		new StatusSegment(OrderTicketStatus.Pending, "Pending", "is-pending"),
		new StatusSegment(OrderTicketStatus.Processing, "Processing", "is-processing"),
		new StatusSegment(OrderTicketStatus.Done, "Done", "is-done"),
		new StatusSegment(OrderTicketStatus.Error, "Error", "is-error")
	];
```
```csharp
	private sealed record StatusSegment(string? Value, string Label, string Modifier);
```

`OrderTicketStatus` is a **duplicate of the backend `TicketStatus`**, in
`UI/FrontendWebassembly/DTO/ATS/OMSTicketingDTO.cs:66-84` (**C6/D6**):

```csharp
// Mirrors ATS.Constants.TicketStatus, which lives in the backend assembly and is not
// referenced by the UI project.
public static class OrderTicketStatus
{
	public const string Pending = "Pending";

	public const string Processing = "Processing";

	public const string Done = "Done";

	public const string Error = "Error";

	// Mirrors OMSTicketingRepository.MaxTicketAttempts. Once an order has used this many
	// automatic attempts the job stops picking it up, which is when a person may retry
	// it by hand.
	public const int MaxAttempts = 5;
}
```

The WASM project does not reference the ATS assembly, so **nothing at compile time keeps these two
vocabularies or the two `5`s in sync**. Rename a status on the backend and the UI filter chip
silently returns zero rows. This is the single most fragile coupling in the feature — see §7.

### 6.3 Loading the table

`.razor.cs:35` holds the cursor state:
`private readonly CursorTableLoader<TicketedOrderListDTO> _ordersLoader = new();`, referenced from
markup as `CursorPagerState="_ordersLoader"`.

The MudTable `ServerData` callback (`LoadServerData="LoadOrdersAsync"`, lines 82-124):

```csharp
	private async Task<TableData<TicketedOrderListDTO>> LoadOrdersAsync(
		TableState state,
		CancellationToken cancellationToken)
	{
		// Every input that invalidates the keyset walk must be in the signature.
		var signature = string.Join(
			'|',
			_activeStatus,
			_searchString,
			_dateRange?.Start?.ToString("yyyy-MM-dd"),
			_dateRange?.End?.ToString("yyyy-MM-dd"));

		var tableData = await LoadCursorPagedDataAsync(
			_ordersLoader,
			state,
			signature,
			(cursor, pageSize) => OMSTicketingService.GetTicketedOrdersAsync(
				cursor,
				pageSize,
				_activeStatus,
				_searchString,
				_dateRange?.Start,
				_dateRange?.End));
```

The `signature` is what tells the cursor loader that a filter changed and the walk must restart from
page one. **Forget to include a new filter in that join and paging silently continues the old walk**
with the new filter applied — the classic keyset-paging bug.

Counts are fetched separately and re-fetched at the end of every table load so chips cannot drift:

```csharp
			var response = await OMSTicketingService.GetStatusCountsAsync(
				_searchString,
				_dateRange?.Start,
				_dateRange?.End);
```

Retry eligibility, matching the design doc's rule:

```csharp
	// A manual retry is only offered once the job has exhausted its automatic attempts.
	// Below the cap the order is still queued, so a button would be redundant.
	private static bool CanRetry(TicketedOrderListDTO order) =>
		IsError(order) && order.TicketAttempts >= OrderTicketStatus.MaxAttempts;
```

The table declares **`ColumnCount="8"`** (**C9**) — the seven data columns plus a leading `Select`
checkbox column for bulk retry.

### 6.4 Retry, and the double-click guard

```csharp
	// Disables the row's button while its retry is in flight, so a double-click cannot
	// queue the same order twice.
	private Guid? _retryingOrderId;
```

```csharp
	private async Task<bool> RetryTicketAsync(Guid emailInvitationId)
	{
		_retryingOrderId = emailInvitationId;
		await InvokeAsync(StateHasChanged);

		try
		{
			var response = await OMSTicketingService.RetryTicketAsync(emailInvitationId);

			if (!response.IsSuccess || !response.Data)
			{
				// A 409 means the row moved on since the page was loaded, so the list is
				// refreshed either way to show its real state.
				Snackbar.Add(
					response.IsSuccess ? "Failed to queue the order for ticketing." : response.ErrorDetail,
					Severity.Error);

				await ReloadTableAsync();

				return false;
			}

			Snackbar.Add("The order has been queued for ticketing.", Severity.Success);

			// The row moves out of Error and the chip counts change, so both are reloaded.
			await ReloadTableAsync();

			return true;
		}
		finally
		{
			_retryingOrderId = null;
			await InvokeAsync(StateHasChanged);
		}
	}
```

The guard is completed in markup by comparing ids, so **only that row's button locks**:

```razor
<button type="button"
        class="ats-cell-action"
        disabled="@(_retryingOrderId == order.EmailInvitationID)"
        title="Queue this order for OMS ticketing again"
        aria-label="@($"Retry OMS ticketing for {FullName(order)}")"
        @onclick="@(() => ConfirmRetryTicketAsync(order))">
    <MudIcon Icon="@Icons.Material.Outlined.Refresh" Size="Size.Small" />
</button>
```

Non-retryable rows render `<span class="ats-cell-muted" aria-hidden="true">—</span>`, and the Error
pill carries the attempt count so the reason the button appeared is visible:

```razor
@if (IsError(order))
{
    <span class="ticketing-attempts">
        @order.TicketAttempts/@OrderTicketStatus.MaxAttempts
    </span>
}
```

Client-side guard plus server-side `WHERE` clause (§5.1) are **both** needed: the UI guard prevents
an accidental double-queue from one browser, the SQL guard prevents it from two browsers, or from a
browser racing the job.

The bulk variant uses a separate `_isBulkRetrying` bool and reports a partial result as
`Severity.Info`, not an error:

```csharp
			if (result.IsComplete)
			{
				Snackbar.Add(
					$"{result.RequeuedCount} order(s) queued for ticketing.",
					Severity.Success);
			}
			else
			{
				Snackbar.Add(
					$"{result.RequeuedCount} of {result.RequestedCount} order(s) queued. "
						+ "The rest were already back in the queue.",
					Severity.Info);
			}
```

Bulk selection is deliberately **page-scoped and pruned on every reload** —
`_selectedInvitationIds.RemoveWhere(id => !stillSelectable.Contains(id))` — the UI-side counterpart
to the server's per-row `IsWithinScope` filtering. A selection can never outlive the page it was
made on.

The confirm dialog (`ConfirmRetryTicketAsync`) uses the shared
`Component/Generic/YesNoDialogComponent.razor.cs` (`ConfirmActionAsync` parameter at line 39,
invoked at line 89), with warning styling matching the withdrawn-list resend:

```csharp
		var confirmParam = new DialogParameters
		{
			{ nameof(YesNoDialogComponent.Title), "Retry Ticketing" },
			{ nameof(YesNoDialogComponent.Message), $"This will queue {FullName(order)}'s order to be sent to OMS again." },
			{ nameof(YesNoDialogComponent.ConfirmText), "Retry" },
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"Automatic retries have already been used up for this order. Make sure the "
					+ "cause has been fixed, otherwise it will fail again."
			},
			{ nameof(YesNoDialogComponent.ConfirmIcon), Icons.Material.Outlined.Refresh },
			{
				nameof(YesNoDialogComponent.ConfirmActionAsync),
				(Func<Task<bool>>)(() => RetryTicketAsync(order.EmailInvitationID))
			},
			{ nameof(YesNoDialogComponent.AvatarIcon), Icons.Material.Filled.WarningAmber },
			{ nameof(YesNoDialogComponent.AvatarColor), Color.Warning },
			{ nameof(YesNoDialogComponent.InfoColor), Color.Warning },
			{ nameof(YesNoDialogComponent.InfoBGColor), "var(--c-warn-bg)" },
			{ nameof(YesNoDialogComponent.ThemeButtonColor), "theme-button-warning" }
		};
```

The `(Func<Task<bool>>)` cast is **load-bearing** — without it the lambda is ambiguous and the file
does not compile. `ConfirmBulkRetryAsync` repeats the block with plural wording. Note
`InfoBGColor` uses the `--c-warn-bg` token rather than a hex literal, per
`docs/ui-theming-and-responsiveness.md`.

### 6.5 UI service — `Services/ATS/OMSTicketing/`

```csharp
	public OMSTicketingService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}
```

The `"API"` named client is the gateway-facing one (configured with `CookieHandler` +
`InterceptorHandler` in `ServiceConfig/FrontendServiceConfig.cs`; registered
`AddScoped<IOMSTicketingService, OMSTicketingService>()` at line 85). The four URL strings are
**relative to the gateway with no leading slash**, matching `MatchPath` in §6.6:

| Call | Verbatim |
|---|---|
| orders | `var query = $"ats/getticketedorders?pageSize={pageSize}";` then `&cursor=`, `&status=`, `&searchTerm=`, `&startDate=`, `&endDate=`, each `Uri.EscapeDataString`-escaped → `_httpClient.GetAsync(query)` |
| counts | `var query = "ats/getticketstatuscounts";` with a `var separator = '?';` flipped to `'&'` → `_httpClient.GetAsync(query)` |
| single retry | `var request = new { emailInvitationId };` → `_httpClient.PatchAsJsonAsync("ats/retryticket", request)` |
| bulk retry | `var request = new { emailInvitationIds };` → `_httpClient.PatchAsJsonAsync("ats/retrytickets", request)` |

Dates serialise as `"yyyy-MM-dd"`. Responses deserialise through wrapper records
`GetTicketedOrdersResponseDTO` / `GetTicketStatusCountsResponseDTO` that must match the endpoint
response records' property names (§3.1) — except retry, which reads a bare `bool` because the
endpoint returns `Results.Ok(response.Success)`.

Every method rethrows `OperationCanceledException` and folds
`HttpRequestException or JsonException or NotSupportedException` into a `ServiceResponse.Failure`;
non-success statuses surface the server's `detail` via `await response.ReadErrorDetailAsync()`.
**That is the only path by which the 409 message from §5 reaches the snackbar.**

### 6.6 Gateway routes — `Path/ATSPaths.cs` lines 403-445

All four, under the `// ---- Web console ---` comment, every one on
`GatewayConstants.OnePlatformApi`:

```csharp
			new RouteDefinitionDTO(
				RouteId: "GetTicketedOrders",
				MatchPath: "/ats/getticketedorders",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getticketedorders" }
				}
			),
```

and identically for `GetTicketStatusCounts` (`GET`, `/ats/getticketstatuscounts`), `RetryTicket`
(`Patch`, `/ats/retryticket`) and `RetryTickets` (`Patch`, `/ats/retrytickets`).

**Unlike the route immediately preceding them (line ~397), these four carry no `RateLimitPolicy`
metadata**, so they fall through to the gateway's 500/s default. That is fine for a staff console
behind authentication; it would not be for a public route.

Verify all four at runtime with `GET /__routes` on the gateway.

### 6.7 CSS is shared, not copied

`wwwroot/css/ats.css` (6658 lines) holds ~60 `.ats-status-board-*` rules scoped under
`.ats-management-page` — intro banner, filter chips, segmented control, selection bar, date range,
status dots and pills, lead-identity cell, tag and muted cells — plus `.ats-cell-action` at line
1329 with `:hover:not(:disabled)`, `:disabled`, `:focus-visible`, `svg` and an `.is-danger` variant.

The ticketing scoped stylesheet is **55 lines** (**C9**, not 45) and opens with the rule that keeps
it that way:

```css
/* Only what is specific to the ticketing board lives here.

   The intro banner, filter chips, status dots, status pills, lead-identity cell, tag
   and muted cells all come from the shared .ats-status-board-* / .ats-cell-* /
   .ats-status-pill rules in wwwroot/css/ats.css, which Bulk Uploads Status uses too.
   Do not re-declare any of those here - change the shared rules instead so both
   boards stay identical. */
```

Its five selectors: `::deep .ticketing-number` (monospaced — the one value users copy out of this
screen), `::deep .ticketing-status-cell` (stacked), `::deep .ticketing-attempts`,
`::deep .ticketing-error` (`-webkit-line-clamp: 2`, `max-width: 260px`, with
`title="@order.TicketError"` in markup supplying the full text on hover), and a
`@media (max-width: 720px)` override relaxing `.ticketing-error` to `max-width: 100%`.

`BulkUploadsComponent.razor.css` is **95 lines** (not 92), down from ~360 before it was migrated
onto the shared classes in the same change. The `.razor` header repeats the constraint:

```razor
@* Layout, chips and status pills come from the shared .ats-status-board-* rules in
   wwwroot/css/ats.css, the same ones Bulk Uploads Status uses. Only the ticket-number
   and failure-reason cells are specific to this screen. *@
```

---

## 7. Sharp edges

### 7.1 An unconfigured OMS connection string burns the whole retry budget

`OMS/Data/Connection/OMSSqlConnectionFactory.cs`:

```csharp
public sealed class OMSSqlConnectionFactory(string connectionString) : IOMSSqlConnectionFactory
{
	public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
	{
		// Validated lazily instead of at registration so hosts without an OMS
		// secret (e.g. the Testing environment) can still boot.
		if (string.IsNullOrWhiteSpace(connectionString) ||
			connectionString.StartsWith("${", StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"The OMS_Connection connection string is not configured.");
		}

		var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);

		return connection;
	}
}
```

Lazy validation is the right call — it lets the Testing environment boot without an OMS secret. But
`InvalidOperationException` is **neither** `BadRequestException` **nor** caught specially, so
§2.11's generic `catch (Exception ex) when (ex is not OperationCanceledException)` classifies it as
**retryable**. On a host where `OMS_Connection` is missing or still an unresolved `${...}`
placeholder, every order will consume all five attempts across ~50 seconds and park as `Error` with
a configuration message as its `TicketError`.

That is survivable (the orders are recoverable via Retry once configured) but it is a poor failure
mode: a configuration error presents as five thousand business-data errors. If this ever bites, the
fix is to classify `InvalidOperationException` from the connection factory as non-retryable, or to
fail the whole batch before claiming rather than per order.

### 7.2 `CurrentUser` claim precedence is the reverse of what the design doc says (C8)

`Auth/Shared/Implementations/CurrentUser.cs`:

```csharp
	public string? FirstName => GetClaimValue(ClaimTypes.GivenName, AuthClaimTypes.FirstName);

	public string? MiddleName => GetClaimValue(AuthClaimTypes.MiddleName);

	public string? LastName => GetClaimValue(ClaimTypes.Surname, AuthClaimTypes.LastName);
```

with

```csharp
	private string? GetClaimValue(params string[] claimTypes)
	{
		foreach (var claimType in claimTypes)
		{
			var value = Principal?.FindFirst(claimType)?.Value;
			if (!string.IsNullOrWhiteSpace(value))
				return value;
		}

		return null;
	}
```

`ClaimTypes.GivenName` / `ClaimTypes.Surname` are tried **first**; the custom `firstName` /
`lastName` claims are the fallback. Invisible today because `JWTService.GetClaims` never emits the
standard claim types (its only standard claim is `ClaimTypes.NameIdentifier`), so the first lookup
always misses. It would matter for a token issued by any other party — notably the **SAML2 SSO**
path, where an external IdP commonly does emit `GivenName`/`Surname`, and those would then win over
the platform's own stored name parts.

`MiddleName` has **no** standard-claim fallback at all.

Emission side, `Auth/Services/Login/JWTService.cs` inside
`private IEnumerable<Claim> GetClaims(LoginDTO loginDTO, int? sessionId)`:

```csharp
			// The parts as well as the join: callers that must address the user by
			// first/last name separately cannot safely split fullName back apart.
			new Claim(AuthClaimTypes.FirstName, loginDTO.FirstName),
			new Claim(AuthClaimTypes.LastName, loginDTO.LastName),
```

and conditionally, after the list initialiser:

```csharp
		if (!string.IsNullOrEmpty(middle))
			claims.Add(new Claim(AuthClaimTypes.MiddleName, middle));
```

So `middleName` is **absent entirely** (not present-but-empty) for users with no middle name, while
`firstName`/`lastName` are always emitted. Claim names live in `Auth/Constants/AuthClaimTypes.cs`
as `"firstName"`, `"middleName"`, `"lastName"`.

The ticketing job does **not** depend on any of this — it reads the Auth directory via
`IAuthQueries` (§2.8). But tokens issued before these claims existed return null for them, which is
the design doc's "Not done" item 5, and `ICurrentUser` documents exactly that:
*"Null on tokens issued before these claims existed."*

---

## 8. Wiring — what is registered where

`BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs`, all inside `AddATSServices`:

```csharp
	// Same reasoning: TicketStatus moves within one Quartz tick, and the claim
	// query must never be served from a cache.
	services.AddScoped<IOMSTicketingRepository, OMSTicketingRepository>();
```

```csharp
	services.AddScoped<IOMSTicketingProcessorService, OMSTicketingProcessorService>();
	services.AddScoped<IOMSTicketingMonitoringService, OMSTicketingMonitoringService>();
```

```csharp
	services.ConfigureOptions<OMSTicketingBackgroundJobSetup>();
```

plus the order-history pair the retry path depends on (lines 96-97):

```csharp
	services.AddScoped<IOrderHistoryFactory, OrderHistoryFactory>();
	services.AddScoped<IOrderHistoryService, OrderHistoryService>();
```

In the OMS module, `OMS/ServiceConfig/OMSServiceConfiguration.cs:56`:

```csharp
	services.AddScoped<IOMSTicketCreator, OMSTicketCreator>();
```

### 8.1 The ticketing repository is deliberately NOT cached

The only `Decorate` call in the file targets the aggregate:

```csharp
	services.AddScoped<IATSRepository, ATSRepository>();
	services.Decorate<IATSRepository, ATSCacheRepository>();
```

Every other ATS repository is registered by forwarding through that decorated aggregate —
`AddScoped<IXxxRepository>(provider => provider.GetRequiredService<IATSRepository>())` — and
therefore inherits caching. `IOMSTicketingRepository` is registered **directly**, bypassing the
decorator entirely.

That is intentional and the reasoning is in the registration comment: `TicketStatus` moves from
`Pending` to `Done` within a single 10-second tick, so a cached page would show precisely the
staleness this screen exists to remove. Same reasoning as `BulkUploadRepository`. **Do not
"normalise" this registration to match its siblings** — you would cache a queue.

### 8.2 Project reference

`BackendAPI/Modules/ATS/ATS.csproj`:

```xml
	<ItemGroup>
	 <ProjectReference Include="..\..\BuildingBlocks\BuildingBlocks\BuildingBlocks.csproj" />
	 <ProjectReference Include="..\Auth\Auth.csproj" />
	 <ProjectReference Include="..\OMS\OMS.csproj" />
	</ItemGroup>
```

No cycle: `OMS.csproj` references only `BuildingBlocks` (plus `Carter`, `MediatR`,
`Microsoft.Data.SqlClient` 6.1.1 and a `FrameworkReference` to `Microsoft.AspNetCore.App`). Both are
`net10.0`. Before this reference existed, `IOMSTicketCreator` had no consumer outside its own module
and its tests.

---

## 9. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| A `TicketStatus` string constant (`ATS/Constants/TicketStatus.cs`) | `OrderTicketStatus` in `UI/FrontendWebassembly/DTO/ATS/OMSTicketingDTO.cs:66` | Hand-synced duplicate across an assembly boundary; nothing enforces it (§6.2) |
| `MaxTicketAttempts` (repository) | `OrderTicketStatus.MaxAttempts` (UI) | The `5/5` pill and the `CanRetry` gate both read the UI copy (§6.3) |
| The claim SQL's placeholder order | The `FromSqlRaw` argument list | Positional `{0}`–`{6}`; `{0}`/`{1}` are the *written* values and appear first in the args but last in the SQL (§2.5) |
| `ClaimBatchSize` or `MaxDegreeOfParallelism` | `StaleClaimTimeout` (30 min) | The sweeper must outlast the worst-case batch or it steals live rows and duplicates tickets (§2.6) |
| A Carter route string (`MapGet("getticketedorders")`) | `PathSet` in `Path/ATSPaths.cs` **and** the URL literal in `Services/ATS/OMSTicketing/OMSTicketingService.cs` | Three independent string literals in three assemblies (§6.5, §6.6) |
| An endpoint response record's property name | The UI's `*ResponseDTO` wrapper | JSON binding by name; a mismatch is a silent deserialise failure, not a compile error (§3.1) |
| `AtsModuleIds.TicketingStatus` (14) | `ShareData/ATS/ModuleList.cs:31`, `Data/DataSeed/ATSInitialData.cs:472`, `IsPrimaryNavigationModule`, and both `BackfillModuleGrantedWithNewOrderAsync` call sites | Four places must agree for the page to be reachable at all (§6.1, §10) |
| The `@page` route's last segment | The `path` in `ModuleList.cs` | `ATSLayout` matches the URL segment against that string (§6.1) |
| `TicketStatus` nullability or length | The migration and `ATSDBContextModelSnapshot` | `IsRequired(false)` is why legacy orders are unqueued (§1.2) |
| `TicketError` column length (500) | The truncation in `MarkTicketFailedAsync` | A longer message throws on write and masks the real failure (§2.11) |
| Anything adding a filter to the ticketed-orders screen | The `signature` join in `LoadOrdersAsync` | A filter missing from the signature continues the old keyset walk (§6.3) |
| `CreateOMSTicketRequest`'s parameter list | `OMSRepository.CreateTicketAsync`'s bindings | 21 positional parameters bound by hand; `@p_coutry_id` is misspelled **on purpose** (§2.10) |
| `IOMSTicketCreator.CreateTicketAsync`'s signature | `OMS/Features/Tickets/Command/CreateTicket/CreateTicketHandler.cs:88` | The other caller relies on `referenceNumber` defaulting to `""` (§2.10) |
| The catch blocks in `ProcessOneAsync` | §7.1 | Only `BadRequestException` and `OperationCanceledException` are special-cased today |
| `.ats-status-board-*` or `.ats-cell-action` in `ats.css` | `BulkUploadsComponent` | Both boards share those rules by design (§6.7) |
| `InsertEmailInvitationRequestAsync`'s signature | All **three** callers, including `Features/PublicApi/CreateEndorsement` | The design doc lists only two (§2.1) |

---

## 10. Module 14 must agree in four places, not three

The design doc says three. `IsPrimaryNavigationModule` is the fourth (§6.1).

**(a)** `BackendAPI/Modules/ATS/Constants/AtsModuleIds.cs:22`:

```csharp
	public const int BulkUploads = 13;
	public const int TicketingStatus = 14;
	public const int AuditTrail = 15;
```

**(b)** `UI/FrontendWebassembly/ShareData/ATS/ModuleList.cs:31`:

```csharp
			{ 13, ("bulkuploads", "Bulk Uploads Status", Icons.Material.Filled.CloudUpload) },
			{ 14, ("ticketingstatus", "Ticketing Status", Icons.Material.Filled.ConfirmationNumber) },
			{ 15, ("audittrail", "Audit Trail", Icons.Material.Filled.History) },
```

**(c)** `BackendAPI/Modules/ATS/Data/DataSeed/ATSInitialData.cs:472-479`:

```csharp
		 new()
		 {
			 ModuleId = AtsModuleIds.TicketingStatus,
			 ModuleName = "Ticketing Status",
			 ModuleDescription = "OMS auto-ticketing monitoring module for ATS system.",
			 IsActive = true,
			 CreatedAt = DateTime.UtcNow,
			 UpdatedAt = DateTime.UtcNow
		 },
```

**(d)** The backfill, because `ATSInitialData` only seeds into an **empty** table —
`Data/Extensions/ATSDatabaseExtensions.cs:90-91`, after `SaveChangesAsync`:

```csharp
		await BackfillModuleGrantedWithNewOrderAsync(context, initData, AtsModuleIds.BulkUploads);
		await BackfillModuleGrantedWithNewOrderAsync(context, initData, AtsModuleIds.TicketingStatus);
	}
```

The generalised method (lines 94-105), with the reasoning inline:

```csharp
	// The seed blocks above only run on an empty table, so a module added after the
	// first deployment would never reach an existing database. This backfills one such
	// module and grants it to everyone who can already reach New Order, which is the
	// access rule these monitoring modules follow. Idempotent: a second run adds nothing.
	private static async Task BackfillModuleGrantedWithNewOrderAsync(
		ATSDBContext context,
		ATSInitialData initData,
		int moduleId)
	{
		var moduleExists = await context.ModuleDetails
			.AnyAsync(module => module.ModuleId == moduleId);

		if (!moduleExists)
		{
			var module = initData.GetATSModules()
				.FirstOrDefault(candidate => candidate.ModuleId == moduleId);

			if (module is null)
			{
				return;
			}

			await context.ModuleDetails.AddAsync(module);
			await context.SaveChangesAsync();
		}

		// One access row per user per module, so the grant is modelled as a copy of the
		// user's New Order row with the module id swapped.
		var newOrderRows = await context.UserDetails
			.AsNoTracking()
			.Where(user => user.ModuleId == AtsModuleIds.NewOrder)
			.ToListAsync();
```

It then subtracts already-granted users (`alreadyGranted.ToHashSet()`), projects new `UserDetails`
rows copying `UserEmail / UserName / RoleId / ClientId / Site / IsActive` from each New Order row
with `ModuleId` swapped, and returns early when `newRows.Count == 0`. Idempotent, so it is safe on
every startup.

**Add module 16 and forget (d), and existing databases never see it** — the screen will work on a
freshly seeded dev database and be invisible in production. That is the failure this method exists
to prevent.
