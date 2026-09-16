# ATS Order Status History — Code Explanation

Companion to [`ats-order-status-history.md`](ats-order-status-history.md). That document explains
*what* the timeline is for and *why* it is business history rather than Serilog output. This one
exists so a developer can change the implementation without opening every file cold: it names the
real method at each hop, quotes the code that carries the correctness, and lists **every** place
that writes a history entry — because the question a maintainer actually has is *"does my new code
path need to write one, and with what event type?"* That is §4.

> **Read §0 first.** The design doc is short and predates several later changes. Nine of its claims
> no longer match the code, including a security property it asserts that was never implemented.
> Everything below was verified against branch `feature/Update-ReadMe-File`; where the two disagree,
> this document follows the code.
>
> `oms-auto-ticketing_code_explanation.md` §5.2 quotes the factory and service from the retry angle.
> **This is their home document** — they are quoted in full in §3.

---

## 0. Where the design doc no longer matches the code

| # | `ats-order-status-history.md` says | The code actually does |
|---|---|---|
| **C1** | "The endpoint is authorized and applies the same ATS client/requestor scope as report access." | **NOT FOUND.** `GetOrderStatusHistoryEndpoint` calls `.RequireAuthorization()` and nothing else. No `IAtsAccessScopeResolver` appears anywhere in the slice, and the repository filters on `EmailInvitationRequestId` alone. Any authenticated user who knows an invitation id can read that order's timeline (§8.1) |
| **C2** | The timeline records "who did it" | Half true. `ChangedByUserId` is **written** by the factory but is **not a member of `OrderStatusHistoryDTO`**, so no API returns it. The `who` is write-only (§3.3) |
| **C3** | Lifecycle table lists six events | There are **seven**. `TicketRetryRequested` (`Constants/OrderHistoryEventType.cs:14`) is missing from the table entirely |
| **C4** | "Initial report uploads do not record completion; only an upload that actually moves the order to `Completed` does." | True only on the **first-upload** branch (`ReportService.cs:141`, guarded). The **re-upload** branch (`ReportService.cs:107`) records `ReportUploaded → Completed` unconditionally, even when `orderStatus` is still `In Progress` (§8.3) |
| **C5** | `ApplicationFormResent`: previous = "Application Withdrawn", new = "Pending Candidate Info" | The single resend passes `invitation.OrderStatus` — whatever it currently is, not necessarily Withdrawn. The **bulk** resend passes `null`. Two shapes for one event type (§4.3) |
| **C6** | `ReportDisputed`: previous = Completed, new = Completed | `DisputeOrderService.cs:159` writes `order.OrderStatus` on the previous side and `order.OrderStatus ?? OrderStatus.Completed` on the new. When the status is null the entry reads `null → "Completed"` — not the symmetric pair described |
| **C7** | Background jobs should write with `OrderHistorySource.System` when the job causes the transition | `OrderHistorySource.System` is **never referenced in any `.cs` file**. The bulk parsing job uses `file.Source ?? OrderHistorySource.Web`. The constant is dead |
| **C8** | "Adding another lifecycle event … 3. Add the user-facing title, description, icon, and tone in `OrderStatusHistoryDialog`." | Step 3 was **not done for `TicketRetryRequested`**. It is absent from all four switch expressions, so it renders as the raw constant with the fallback description (§7.2) |
| **C9** | Only one read path is described (the UI dialog) | There are **three**. The public API embeds the timeline in `PublicOrderDetailDTO.History`, and the Withdrawn Applications screen derives its `WithdrawnAt` column *from this table*. History is load-bearing (§6) |

Two claims the doc gets right and that are easy to break: the chain `Carter endpoint → MediatR query
handler → order-history service → repository → ATSDBContext` is exactly what §5 traces, and there is
exactly one migration for the table
(`BackendAPI/API/APIs/Migrations/ATS/20260813034230_AddOrderStatusHistory.cs`). The three later
`*AtsAuditTrail*` migrations touch a different table; their Designer snapshots merely re-emit this
one's shape.

---

## 1. The data model

### 1.1 Entity — `BackendAPI/Modules/ATS/Data/Entities/OrderStatusHistory.cs`

The whole file. A plain POCO; no mapping attributes, everything is fluent (§1.2).

```csharp
namespace ATS.Data.Entities;

public class OrderStatusHistory
{
	public Guid OrderStatusHistoryId { get; set; }
	public Guid EmailInvitationRequestId { get; set; }
	public string EventType { get; set; } = string.Empty;
	public string? PreviousStatus { get; set; }
	public string NewStatus { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;
	public DateTime OccurredAt { get; set; }
	public Guid? ChangedByUserId { get; set; }
	public EmailInvitationRequest EmailInvitationRequest { get; set; } = null!;
}
```

The nullability split is the shape of the feature: `PreviousStatus` is the only nullable string,
because the first event in an order's life has nothing before it. `NewStatus` is required — every
entry must say where the order ended up, even when (as with a ticket retry) that is where it started.
The inverse navigation is `EmailInvitationRequest.cs:62`
(`public ICollection<OrderStatusHistory>? OrderStatusHistories { get; set; }`), and **nothing ever
`.Include()`s it** — all three read paths query the `DbSet` directly and project to a DTO.

### 1.2 EF configuration — `Data/EntityConfiguration/OrderStatusHistoryConfiguration.cs`

Note the folder: **`Data/EntityConfiguration/`**, not `Data/Configurations/`. Picked up by
`modelBuilder.ApplyConfigurationsFromAssembly(typeof(ATSDBContext).Assembly);` in
`Data/Context/ATSDBContext.cs` — nothing is configured inline in the DbContext. The whole file:

```csharp
public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
	public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
	{
		builder.ToTable("OrderStatusHistory", "ats");
		builder.HasKey(x => x.OrderStatusHistoryId);
		builder.Property(x => x.OrderStatusHistoryId).ValueGeneratedNever();
		builder.Property(x => x.EventType).HasMaxLength(80).IsRequired();
		builder.Property(x => x.PreviousStatus).HasMaxLength(255);
		builder.Property(x => x.NewStatus).HasMaxLength(255).IsRequired();
		builder.Property(x => x.Source).HasMaxLength(40).IsRequired();
		builder.Property(x => x.OccurredAt).IsRequired();
		builder.HasIndex(x => new { x.EmailInvitationRequestId, x.OccurredAt });
		builder.HasOne(x => x.EmailInvitationRequest)
			.WithMany(x => x.OrderStatusHistories)
			.HasForeignKey(x => x.EmailInvitationRequestId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
```

Four things to notice:

- **`ValueGeneratedNever()` on the key.** The id is a `Guid.CreateVersion7()` minted in the factory
  (§3.1), not a database default. v7 is time-ordered, so inserts append at the end of the primary
  key index instead of scattering through it — which matters on a table this write-hot.
- **Exactly one index**, composite `(EmailInvitationRequestId, OccurredAt)` — precisely the shape of
  both timeline reads: filter by order, sort by time. There is **no index on `EventType`**, which is
  why the Withdrawn Applications correlated subquery (§6.2) seeks on invitation id and filters the
  event type afterwards.
- **`Cascade` delete.** Deleting an order deletes its timeline, so the history is *not*
  independently durable and calling it an "audit trail" overstates it (§8.5).
- **No FK and no index on `ChangedByUserId`.** The acting user is a bare `uuid` with no relationship
  to Auth's `UserDetails`. Nothing joins it, and nothing can.

### 1.3 Migration — `Migrations/ATS/20260813034230_AddOrderStatusHistory.cs`

Namespace `APIs.Migrations.ATS`, matching the module-scoped migrations folder. `Up()` creates the
table plus the one index; `Down()` drops the table.

```csharp
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
```

`OccurredAt` is `timestamptz`, so every value must be UTC — the factory's `DateTime.UtcNow` is not a
stylistic choice, it is what keeps Npgsql from throwing.

---

## 2. The three vocabularies

Three independent string vocabularies meet in one row. None is an enum. Two are
`public static class` of `const string`; `OrderStatus` is `internal`.

### 2.1 Event types — `Constants/OrderHistoryEventType.cs`

The whole file:

```csharp
namespace ATS.Constants;

public static class OrderHistoryEventType
{
	public const string OrderCreated = "OrderCreated";
	public const string ApplicationFormSubmitted = "ApplicationFormSubmitted";
	public const string ApplicationFormWithdrawn = "ApplicationFormWithdrawn";
	public const string ApplicationFormResent = "ApplicationFormResent";
	public const string ReportUploaded = "ReportUploaded";
	public const string ReportDisputed = "ReportDisputed";

	// A person put an order whose automatic OMS retries were exhausted back on the
	// ticketing queue. The order's own status does not change; this records who did it.
	public const string TicketRetryRequested = "TicketRetryRequested";
}
```

There is **no `All` array** here, unlike `TicketStatus.All`, and nothing validates an event type on
write: `RecordAsync` takes a bare `string eventType`, so a typo compiles, inserts, and silently
renders in the UI as the raw string via the dialog's `_ => eventType` fallback (§7.2). The constants
are the only guard, and they are advisory.

### 2.2 Channel — `Constants/OrderHistorySource.cs`

The whole file:

```csharp
namespace ATS.Constants;

public static class OrderHistorySource
{
	public const string Web = "Web";
	public const string PublicApi = "PublicApi";
	public const string System = "System";
}
```

`Web` is the **default parameter value** on every `Record*` overload, so a caller that does not think
about provenance gets `Web`. In practice:

| Member | Actually used by |
|---|---|
| `Web` | Every call site except the two below — including the AI assistant's order creation (`AtsAssistantService.cs:380` passes no `source`) and the bulk job's fallback |
| `PublicApi` | `CreateEndorsementHandler.cs:91` (creation) and `PublicApiService.cs:99` (withdrawal) |
| `System` | **Nothing.** Dead constant — see **C7** |

### 2.3 Lifecycle statuses — `Constants/OrderStatus.cs`

The whole file:

```csharp
namespace ATS.Constants;

internal static class OrderStatus
{
	internal const string PendingCandidateInfo = "Pending Candidate Info";
	internal const string InProgress = "In Progress";
	internal const string ApplicationWithdrawn = "Application Withdrawn";
	internal const string Completed = "Completed";
}
```

`internal`, with `internal const` members — **this vocabulary cannot leave the ATS assembly.** That
is why `PreviousStatus`/`NewStatus` are plain `string` on the entity and DTO, and why the UI keeps a
hand-copied duplicate (§7.3). Note also that these are **display strings with spaces**, stored in the
database exactly as a human reads them: renaming `"Pending Candidate Info"` is a data migration, not
a refactor.

### 2.4 How the three relate

An event type is the *verb*; the status pair is the *edge* in the lifecycle graph; the source is the
*channel*. Legal combinations, as actually written by the twelve call sites in §4:

| Event type | Previous | New | Real transition? |
|---|---|---|---|
| `OrderCreated` | `null` | `Pending Candidate Info` | Yes — birth |
| `ApplicationFormSubmitted` | `Pending Candidate Info` (**hardcoded**) | `In Progress` | Yes |
| `ApplicationFormWithdrawn` | the order's current status | `Application Withdrawn` | Yes |
| `ApplicationFormResent` | current status (single) / `null` (bulk) | `Pending Candidate Info` | Yes — the repository really does reset `OrderStatus` |
| `ReportUploaded` | the order's current status | `Completed` | Yes, when guarded |
| `ReportDisputed` | the order's current status | the same, or `Completed` if null | **No** — deliberately |
| `TicketRetryRequested` | the order's current status (single) / `null` (bulk) | the same, or `""` (bulk) | **No** — deliberately |

Two event types are *not* lifecycle steps and record the same value on both sides so the timeline
does not imply movement. That convention is §4.3.

---

## 3. The writer — factory, service, repository

### 3.1 `Services/OrderHistory/OrderHistoryFactory.cs`

The whole file:

```csharp
namespace ATS.Services.OrderHistory;

public class OrderHistoryFactory : IOrderHistoryFactory
{
	private readonly ICurrentUser _currentUser;

	public OrderHistoryFactory(ICurrentUser currentUser) => _currentUser = currentUser;

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
}
```

The factory does four jobs and nothing else: mint a v7 id, stamp UTC now, resolve the acting user,
normalise `Guid.Empty` to `null`. It validates nothing — not the event type, not the status pair, not
whether the invitation exists.

**Why `changedByUserId` exists.** `ICurrentUser.UserId` is `Guid?`
(`Auth/Shared/Contracts/ICurrentUser.cs:6`) and its implementation
(`Auth/Shared/Implementations/CurrentUser.cs:16`) parses the claim off
`IHttpContextAccessor.HttpContext.User`. On a Quartz worker thread there is no `HttpContext`, so
`UserId` is null, so `ChangedByUserId` would be written null — and the entry would record that a bulk
file created three hundred orders with nobody responsible. The parameter lets the job name the human
who *originally* uploaded the file, recovered from persisted data instead of the ambient request. The
`userId == Guid.Empty ? null : userId` guard covers the opposite failure: a caller that *does* have a
context but resolves an empty id (`AtsAssistantPlugin.cs` does exactly this with
`currentUser.UserId ?? Guid.Empty`). An all-zero value is worse than null because it reads as a real
user.

### 3.2 `Services/OrderHistory/OrderHistoryService.cs`

The whole file:

```csharp
namespace ATS.Services.OrderHistory;

public class OrderHistoryService : IOrderHistoryService
{
	private readonly IOrderHistoryFactory _factory;
	private readonly IOrderHistoryRepository _repository;

	public OrderHistoryService(IOrderHistoryFactory factory, IOrderHistoryRepository repository)
	{
		_factory = factory;
		_repository = repository;
	}

	public Task RecordAsync(Guid invitationId, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web) =>
		_repository.AddAsync(_factory.Create(invitationId, eventType, previousStatus, newStatus, source), cancellationToken);

	public Task RecordManyAsync(IReadOnlyCollection<Guid> invitationIds, string eventType, string? previousStatus, string newStatus, CancellationToken cancellationToken, string source = OrderHistorySource.Web, Guid? changedByUserId = null) =>
		_repository.AddRangeAsync(
			invitationIds.Select(id => _factory.Create(id, eventType, previousStatus, newStatus, source, changedByUserId)).ToList(),
			cancellationToken);

	public Task<IReadOnlyList<OrderStatusHistoryDTO>> GetAsync(Guid invitationId, CancellationToken cancellationToken) =>
		_repository.GetAsync(invitationId, cancellationToken);
}
```

Three things that matter when you add a call site:

1. **`RecordAsync` has no `changedByUserId` parameter.** Only `RecordManyAsync` does. A background job
   writing *one* attributed entry must call `RecordManyAsync` with a single-element collection, or
   lose the attribution. The interface doc (`IOrderHistoryService.cs:8-10`) presents the overload as a
   bulk-performance device — "a single file can create hundreds of orders at once" — not as the only
   attributed path, which is why the asymmetry is easy to miss (§8.4).
2. **`RecordManyAsync` applies one status pair to every id.** It cannot vary per order, so a bulk
   caller either knows they are all the same or writes `null`. That is exactly why the bulk resend
   and bulk retry pass `null` where their single-order twins pass the real value (**C5**, §4.3).
3. **The service opens no transaction and does no scoping.** It is a pass-through; transactional
   coupling is entirely the caller's job, and the callers differ (§4, "Transaction" column).

### 3.3 Repository, and the DTO that drops a column

`Data/Repository/OrderHistory/ATSRepository.OrderHistory.cs`, whole file:

```csharp
namespace ATS.Data.Repository;

public partial class ATSRepository
{
	public async Task AddAsync(OrderStatusHistory history, CancellationToken cancellationToken)
	{
		await _dbcontext.OrderStatusHistories.AddAsync(history, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
	}

	// One insert for a whole bulk file. AddAsync above saves per row, which would be a
	// round trip per subject when a single upload can create hundreds of them.
	public async Task AddRangeAsync(IReadOnlyCollection<OrderStatusHistory> histories, CancellationToken cancellationToken)
	{
		if (histories.Count == 0)
		{
			return;
		}

		await _dbcontext.OrderStatusHistories.AddRangeAsync(histories, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<OrderStatusHistoryDTO>> GetAsync(Guid invitationId, CancellationToken cancellationToken) =>
		await _dbcontext.OrderStatusHistories.AsNoTracking()
			.Where(x => x.EmailInvitationRequestId == invitationId)
			.OrderBy(x => x.OccurredAt)
			.Select(x => new OrderStatusHistoryDTO(x.OrderStatusHistoryId, x.EventType, x.PreviousStatus, x.NewStatus, x.Source, x.OccurredAt))
			.ToListAsync(cancellationToken);
}
```

`AddAsync` calls `SaveChangesAsync` itself. When the caller has already opened a transaction via
`_unitOfWork.BeginTransactionAsync`, that save joins it; when it has not, the history commits
autonomously. The read projection carries **six** of the entity's eight columns —
`DTO/OrderStatusHistoryDTO.cs`:

```csharp
public record OrderStatusHistoryDTO(
	Guid OrderStatusHistoryId,
	string EventType,
	string? PreviousStatus,
	string NewStatus,
	string Source,
	DateTime OccurredAt);
```

`ChangedByUserId` is absent. That is **C2**: the factory goes to real trouble to attribute each entry
to a user, and no reader can see the result. `Data/Cache/OrderHistory/ATSCacheRepository.OrderHistory.Cache.cs`
is a pure pass-through — *"OrderHistory was never decorated with caching — pure pass-through preserves
that."* — so history reads are **never cached**, even though `IATSRepository` is decorated and most
other ATS reads are.

---

## 4. Every call site

Twelve distinct writes across seven files. This is the table to consult before adding a new code path.
"Transaction" says whether the write joins a caller-opened transaction or commits autonomously.

| # | Event type | File:line | Source | previous → new | Transaction |
|---|---|---|---|---|---|
| 1 | `OrderCreated` | `Services/EndorsementSubmission/EndorsementSubmissionService.cs:186` | parameter, default `Web` | `null` → `PendingCandidateInfo` | `TransactionRunner.RunAsync` |
| 2 | `ApplicationFormResent` | `Services/EndorsementSubmission/EndorsementSubmissionService.cs:526` | default `Web` | `invitation.OrderStatus` → `PendingCandidateInfo` | none (after requeue commit) |
| 3 | `ApplicationFormResent` | `Services/EndorsementSubmission/EndorsementSubmissionService.cs:625` | default `Web` | `null` → `PendingCandidateInfo` | none |
| 4 | `ApplicationFormSubmitted` | `Services/ApplicationForm/ApplicationFormService.cs:113` | default `Web` | `PendingCandidateInfo` (**hardcoded**) → `InProgress` | `_unitOfWork`, pre-commit |
| 5 | `ApplicationFormWithdrawn` | `Services/ApplicationForm/ApplicationFormService.cs:507` | default `Web` | `invitation.OrderStatus` → `ApplicationWithdrawn` | `_unitOfWork`, pre-commit |
| 6 | `ReportUploaded` | `Services/Report/ReportService.cs:107` | default `Web` | `invitation.OrderStatus` → `Completed` | `_unitOfWork`, pre-commit |
| 7 | `ReportUploaded` | `Services/Report/ReportService.cs:141` | default `Web` | `invitation.OrderStatus` → `Completed` | `_unitOfWork`, pre-commit |
| 8 | `ReportDisputed` | `Services/DisputeOrder/DisputeOrderService.cs:159` | default `Web` | `order.OrderStatus` → `order.OrderStatus ?? Completed` | `_unitOfWork`, pre-commit |
| 9 | `ApplicationFormWithdrawn` | `Services/PublicApi/PublicApiService.cs:93` | **`PublicApi`** | `previousStatus` → `ApplicationWithdrawn` | none (after `WithdrawOrderAsync`) |
| 10 | `OrderCreated` | `Services/BulkSubmissionProcessor/BulkSubmissionProcessorService.cs:236` | `file.Source ?? Web` | `null` → `PendingCandidateInfo` | none |
| 11 | `TicketRetryRequested` | `Services/OMSTicketingMonitoring/OMSTicketingMonitoringService.cs:183` | default `Web` | `target.OrderStatus` → `target.OrderStatus ?? ""` | none |
| 12 | `TicketRetryRequested` | `Services/OMSTicketingMonitoring/OMSTicketingMonitoringService.cs:256` | default `Web` | `null` → `""` | none |

### 4.1 Enrolment — `OrderCreated` (sites 1, 10)

Site 1 is `InsertEmailInvitationRequestAsync`, whose signature carries the provenance:

```csharp
	public async Task<bool> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default, string source = OrderHistorySource.Web)
```

Three entry points reach it: `Features/Web/InsertEmailInvitationRequest/InsertEmailInvitationRequestHandler.cs:51`
(no `source`, so `Web`), `Features/PublicApi/CreateEndorsement/CreateEndorsementHandler.cs:88-91`
(explicitly `OrderHistorySource.PublicApi`), and `Services/AIAssistant/AtsAssistantService.cs:380`
(no `source`, so an assistant-created order is indistinguishable in the timeline from a
console-created one). The write sits inside `TransactionRunner.RunAsync` alongside the insert, the
inline SMTP send and the status update, so **there is no window where an order exists without an
`OrderCreated` entry** — a failed send rolls the whole thing back, history included.

Site 10 is the bulk parsing job, and the one place that gets background attribution right:

```csharp
				// Bulk orders previously recorded no history at all, so their timelines
				// started blank while single orders showed OrderCreated. The source is
				// taken from the file because this job has no HTTP context to resolve
				// the caller from.
				if (subjects.Count > 0)
				{
					await scope.ServiceProvider
						.GetRequiredService<IOrderHistoryService>()
						.RecordManyAsync(
							subjects.Select(subject => subject.EmailInvitationID).ToList(),
							OrderHistoryEventType.OrderCreated,
							null,
							OrderStatus.PendingCandidateInfo,
							cancellationToken,
							file.Source ?? OrderHistorySource.Web,
							file.UploadedByUserId);
				}
```

Both `source` and `changedByUserId` come from the persisted `BulkUploadFileDetails` row (`Source` is
`string?` at `Data/Entities/BulkUploadFileDetails.cs:25`), never from `ICurrentUser`. Note the
resolution path too: `scope.ServiceProvider.GetRequiredService<IOrderHistoryService>()` rather than a
constructor field, because the job creates one `IServiceScope` per file inside a `SemaphoreSlim(3)`
fan-out and cannot hold a scoped service on the singleton.

### 4.2 Application form, report, dispute, public API (sites 2–9)

Sites 4 and 5 are the candidate-facing pair, both reached by hash token with **no authenticated user
at all**. Consequently `_currentUser.UserId` resolves null and the subject's own submission and
withdrawal are recorded with `ChangedByUserId = null` — the actor is recoverable from the order row,
not from the history entry. Site 4 also hardcodes its previous status where every other lifecycle
caller reads the actual one:

```csharp
			await _orderHistoryService.RecordAsync(
				emailInvitationId,
				OrderHistoryEventType.ApplicationFormSubmitted,
				OrderStatus.PendingCandidateInfo,
				OrderStatus.InProgress, ct);
```

That is correct today because a form can only be filled in from `Pending Candidate Info`, but it
asserts what the status *ought* to be rather than recording what it was.

Sites 6 and 7 are the two report branches and they are **not** equivalent — site 7 is guarded, site 6
is not (§8.3). Site 8 (`ReportDisputed`) follows `_atsRepository.MarkAsDisputedAsync`, which writes
`DisputeCategory`/`DisputedAt` and never touches `OrderStatus`; hence the faked transition. Note the
dispute email is sent **before** the transaction opens, so a failed commit leaves a sent email and no
history entry. Site 9 is the only call passing `PublicApi` at the call site, and it captures the
previous status into a local *before* the write — the right pattern:

```csharp
		await _orderHistoryService.RecordAsync(
			orderId,
			OrderHistoryEventType.ApplicationFormWithdrawn,
			previousStatus,
			OrderStatus.ApplicationWithdrawn,
			cancellationToken,
			OrderHistorySource.PublicApi);
```

Sites 11 and 12 are covered from the ticketing side in `oms-auto-ticketing_code_explanation.md` §5;
§4.3 below covers the status-pair convention they established.

**Which paths write nothing.** The email delivery job (an invitation moving `Pending → Sent/Error`),
the OMS ticketing job (claiming, succeeding, exhausting — only a *human* retry writes), subject-name
edits, report views, PDF generation and notifications. An order can therefore be ticketed and
delivered with a timeline that says only `OrderCreated`. If you are looking for "who changed the
subject name", this is not the table — `AtsAuditEntry` is.

### 4.3 The previousStatus/newStatus convention

There are two kinds of event, and the pair encodes which kind.

**A lifecycle step** writes the real edge: previous is the order's status before the action, new is
its status after. Sites 1, 4, 5, 6, 7, 9 do this.

**A non-lifecycle action** writes the *same* value on both sides, so the timeline records that
something happened without implying the order moved. The ticketing retry is the canonical case, and
its comment states the rule (`OMSTicketingMonitoringService.cs:180-188`):

```csharp
		// Records who forced the retry. The order's own status is unchanged - this is a
		// ticketing action, not a step in the order lifecycle - so it is written on
		// both sides of the entry.
		await _orderHistoryService.RecordAsync(
			emailInvitationId,
			OrderHistoryEventType.TicketRetryRequested,
			target.OrderStatus,
			target.OrderStatus ?? string.Empty,
			cancellationToken);
```

`Test/.../ATS.UnitTests/OMSTicketingMonitoringServiceTests.cs:72-88` pins exactly this, with the same
reasoning in its comment — the only test in the repository asserting anything about the convention.

**Two callers look wrong.**

Site 12 (bulk ticket retry) writes `null → string.Empty`, not the same value on both sides:

```csharp
			await _orderHistoryService.RecordManyAsync(
				eligibleIds,
				OrderHistoryEventType.TicketRetryRequested,
				null,
				string.Empty,
				cancellationToken);
```

A bulk-retried order therefore gets a materially different entry from a single-retried one, and
`string.Empty` is not a member of `OrderStatus` — any consumer switching on the new status sees a
value that exists nowhere else in the system. The cause is structural: `RecordManyAsync` takes one
pair for the whole batch (§3.2). Fix it by accepting the asymmetry deliberately, or by grouping the
targets by status.

Site 2 (single resend) carries a comment that contradicts its own code
(`EndorsementSubmissionService.cs:523-531`):

```csharp
		// After the requeue committed, so the history reflects work that is actually
		// scheduled. The order's own status is unchanged - queueing an email is not a step
		// in the order lifecycle - so it is written on both sides of the entry.
		await _orderHistoryService.RecordAsync(
			emailInvitationId,
			OrderHistoryEventType.ApplicationFormResent,
			invitation.OrderStatus,
			OrderStatus.PendingCandidateInfo,
			cancellationToken);
```

Here the **code is right and the comment is stale**. `RequeueEmailInvitationAsync`
(`Data/Repository/EmailInvitations/ATSRepository.EmailInvitations.cs:64`) really does
`.SetProperty(x => x.OrderStatus, OrderStatus.PendingCandidateInfo)`, so a resend *is* a lifecycle
step and the asymmetric pair is correct. The comment was evidently copied from the ticket-retry path,
where the claim is true. Site 3 (bulk resend) then writes `null` as previous for the same event type
— the `RecordManyAsync` limitation again.

---

## 5. One flow traced end to end — `GET /ats/getorderstatushistory`

**Browser.** `Component/ATS/Orders/SearchReportComponent.razor:196` — the existing status badge is a
button, so the timeline costs no extra column:

```razor
            <MudTd DataLabel="Status"><button type="button" class="report-status-button" @onclick="@(() => OpenStatusHistoryDialog(r))"><span class="report-status @GetOrderStatusClass(r.OrderStatus)">@GetOrderStatusText(r.OrderStatus)</span></button></MudTd>
```

`SearchReportComponent.razor.cs:301-305` opens the dialog lazily — nothing is fetched until the click:

```csharp
	private async Task OpenStatusHistoryDialog(ReportListDTO report)
	{
		var parameters = new DialogParameters { { nameof(OrderStatusHistoryDialog.EmailInvitationRequestId), report.EmailInvitationRequestId }, { nameof(OrderStatusHistoryDialog.SubjectName), report.SubjectName } };
		await OpenResultDialog<OrderStatusHistoryDialog>(string.Empty, parameters, MaxWidth.Small, noHeader: true);
	}
```

**UI service.** `UI/FrontendWebassembly/Services/ATS/Report/ReportService.cs:245-268`. It lives on the
*Report* service — there is no UI-side order-history service:

```csharp
			var response = await _httpClient.GetAsync(
				$"ats/getorderstatushistory?emailInvitationRequestId={emailInvitationRequestId}",
				cancellationToken);
```

It deserialises into `GetOrderStatusHistoryResponseDTO` and returns `result?.History ?? []`, so a
malformed body reads as an empty timeline rather than a failure. Exceptions are narrowed to
`HttpRequestException or JsonException or NotSupportedException`; `OperationCanceledException` is
rethrown.

**Gateway.** `Path/ATSPaths.cs:10-18` — the first route in the array:

```csharp
			new RouteDefinitionDTO(
				RouteId: "GetOrderStatusHistory",
				MatchPath: "/ats/getorderstatushistory",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getorderstatushistory" }
				}
			),
```

`PathSet` rewrites the path and YARP preserves the query string, which is why the UI's
`?emailInvitationRequestId=` reaches the API untouched. Routes are contributed through
`IReverseProxyModule`, so **nothing in `ApiGateways/` mentions this path** — grepping the gateway
project for it returns zero hits, which is expected and not a missing registration.

**Endpoint.** `Features/Web/GetOrderStatusHistory/GetOrderStatusHistoryEndpoint.cs`. The whole class
is one `MapGet`; the parts that matter are the bound parameter and the absence of anything after
`.WithTags`:

```csharp
		app.MapGet(
			"getorderstatushistory",
			async (
				Guid emailInvitationRequestId,
				ISender sender,
				CancellationToken cancellationToken) =>
			{
				var query = new GetOrderStatusHistoryQuery(
					emailInvitationRequestId);

				var result = await sender.Send(
					query,
					cancellationToken);

				return Results.Ok(
					new GetOrderStatusHistoryResponse(result.History));
			})
		.WithName("GetOrderStatusHistory")
		.WithTags("ATS")
		.RequireAuthorization();
```

`Guid emailInvitationRequestId` binds from the query string by name. `.RequireAuthorization()` with no
policy is **C1** — there is no third call, no scope resolver, no policy name.

**Handler.** `GetOrderStatusHistoryHandler.cs`, whole file — there is no validator:

```csharp
public record GetOrderStatusHistoryQuery(Guid EmailInvitationRequestId) : IQuery<GetOrderStatusHistoryResult>;
public record GetOrderStatusHistoryResult(IReadOnlyList<OrderStatusHistoryDTO> History);

public class GetOrderStatusHistoryHandler(IOrderHistoryService service) : IQueryHandler<GetOrderStatusHistoryQuery, GetOrderStatusHistoryResult>
{
	public async Task<GetOrderStatusHistoryResult> Handle(GetOrderStatusHistoryQuery request, CancellationToken cancellationToken) =>
		new(await service.GetAsync(request.EmailInvitationRequestId, cancellationToken));
}
```

From here: `OrderHistoryService.GetAsync` → `ATSCacheRepository.GetAsync` (pass-through) →
`ATSRepository.GetAsync` (§3.3) → `ATSDBContext.OrderStatusHistories`. No caching, no scoping, no
pagination.

---

## 6. The other two read paths

### 6.1 Public API — history embedded in the order detail

`Data/Repository/PublicApi/PublicApiRepository.cs:50-61`, inside `GetOrderAsync`, surfacing on
`DTO/PublicApiDTO.cs:37` as `PublicOrderDetailDTO.History`:

```csharp
		// Fetched separately rather than as a correlated subquery: the timeline is a
		// second, ordered result set and this keeps the projection above flat.
		order.History = await _dbContext.OrderStatusHistories
			.AsNoTracking()
			.Where(history => history.EmailInvitationRequestId == orderId)
			.OrderBy(history => history.OccurredAt)
			.Select(history => new OrderStatusHistoryDTO(
				history.OrderStatusHistoryId,
				history.EventType,
				history.PreviousStatus,
				history.NewStatus,
				history.Source,
				history.OccurredAt))
			.ToListAsync(cancellationToken);
```

Unlike §5 this path **is** scoped — but the scope is applied to the *order* (`ApplyOrderScope` with
`authorizedClientIds` and `requiredRequestorId`), and the history fetch is gated only by the
`order is null` early return above it. That is sound: no order, no timeline. It is also why **C1**
matters — the public API got this right and the web endpoint did not. The class comment gives the
reason it is uncached, and the same reasoning would apply to §5: *"an integrating client polls these
to watch an order move, so a cached page would report the staleness they are polling to avoid."*

### 6.2 Withdrawn Applications — history as the source of a column

`Data/Repository/WithdrawnApplications/ATSRepository.WithdrawnApplications.cs:42-48`, inside the
keyset-paged projection:

```csharp
						WithdrawnAt = _dbcontext.OrderStatusHistories
							.Where(history => history.EmailInvitationRequestId == eir.EmailInvitationID
								&& history.EventType == OrderHistoryEventType.ApplicationFormWithdrawn)
							.OrderByDescending(history => history.OccurredAt)
							.Select(history => (DateTime?)history.OccurredAt)
							.FirstOrDefault(),
```

**This is the only place where the table is load-bearing rather than informational.** The "Withdrawn
at" value a requestor sees is not a column on the order — it is the `OccurredAt` of the most recent
`ApplicationFormWithdrawn` entry. Three consequences: renaming that event type silently blanks the
column with no compile error anywhere; because site 9 writes the *same* event type, a public-API
withdrawal populates it too (correct, but a coupling worth knowing); and the correlated subquery runs
per row filtering on an unindexed `EventType` (§1.2) — the leading invitation id keeps the seek narrow,
so it is fine at current volumes, but it is the query that degrades first if the table grows
unbounded (§8.2).

`Test/.../ATS.IntegrationTests/GetWithdrawnEmailInvitationRequestsIntegrationTests.cs:162-176` seeds
two `ApplicationFormWithdrawn` rows to cover the "most recent wins" ordering.

---

## 7. Frontend rendering

### 7.1 The dialog — `Component/ATS/Orders/OrderStatusHistoryDialog.razor`

Two files only: `.razor` and `.razor.css`. There is **no `.razor.cs`**; all logic is in an `@code`
block. It fetches on `OnInitializedAsync`, so it is lazy at the *page* level (nothing loads until the
badge is clicked) but eager within itself, and renders four states — `_loading`, `_failed` (with a
Retry button wired back to `LoadAsync`), `_history.Count == 0`, and the timeline.

Each row shows title, description, tone, icon, and a meta line:

```razor
                            <div class="ats-history-meta">
                                @item.OccurredAt.ToLocalTime().ToString("MMMM dd, yyyy - h:mm tt") Â· @item.Source
                            </div>
```

`OccurredAt` is stored UTC and converted in the browser — the only place that happens. `Source` is
printed **verbatim**, so an integrating client sees the raw `PublicApi`, not "Public API". The
separator is byte-level mojibake: the file holds U+00C2 U+00B7 where a single U+00B7 middle dot was
intended, so the browser renders `Â·`. Cosmetic, but it will not fix itself.

**`PreviousStatus` and `NewStatus` are never rendered.** The whole status-pair convention of §4.3 is
invisible in the web UI; only the public API exposes it. Worth knowing before you invest in getting a
pair right — for the web timeline, only `EventType` matters.

### 7.2 Four hand-maintained switch expressions — the duplication hazard

The event-type → presentation mapping lives entirely in this one file, as four parallel `switch`
expressions:

```csharp
    private static string GetTitle(string eventType) => eventType switch
    {
        "OrderCreated" => "Order created",
        "ApplicationFormSubmitted" => "Application form submitted",
        "ApplicationFormWithdrawn" => "Application withdrawn",
        "ApplicationFormResent" => "Application form resent",
        "ReportUploaded" => "Report uploaded",
        "ReportDisputed" => "Report disputed",
        _ => eventType
    };
```

`GetDescription`, `GetTone` and `GetIcon` have the same six-branch shape (`GetTone` maps
`ReportUploaded → "success"`, `ApplicationFormSubmitted → "active"`, `ReportDisputed → "warning"`,
`ApplicationFormWithdrawn → "danger"`, everything else `"neutral"`). Three hazards, all live:

1. **The literals are `"OrderCreated"`, not a shared constant** — and cannot be, since the constants
   live in the ATS backend assembly and this is a Blazor WASM project with no reference to it. A
   renamed event type produces no compile error here.
2. **`TicketRetryRequested` is missing from all four (**C8**).** It falls through to `_ => eventType`,
   so a requestor who forced a retry sees a row titled literally `TicketRetryRequested`, described as
   "The order lifecycle was updated.", in `neutral` tone with a plain circle icon. The design doc's
   own step 3 says to add it; it was not added.
3. **The fallbacks are silent.** `_ => eventType` and `_ => "The order lifecycle was updated."`
   degrade to ugly-but-plausible rather than failing loudly — right for a user-facing dialog, wrong
   for noticing a missing mapping. There is no test, no log, no build warning.

`GetDescription` for `ReportUploaded` also asserts "The final report was uploaded **and the order was
completed**", which is untrue on the unguarded re-upload path (§8.3).

### 7.3 `SharedService/OrderStatusDisplay.cs` — a second hand-synced copy

This is the *status* mapper, used by the badge in `SearchReportComponent.razor:196` and by
`ATSResultComponent` — not the event-type mapper. It carries its own admission:

```csharp
public static class OrderStatusDisplay
{
	// Mirrors BackendAPI/Modules/ATS/Constants/OrderStatus.cs
	public const string Completed = "Completed";
	public const string InProgress = "In Progress";
	public const string PendingCandidateInfo = "Pending Candidate Info";
	public const string ApplicationWithdrawn = "Application Withdrawn";
```

Four constants duplicating an `internal` backend class across an assembly boundary, linked only by a
comment — the same pattern the OMS ticketing doc flags for `OrderTicketStatus`. Because `OrderStatus`
is `internal`, the copy is **unavoidable** without promoting the backend constants or adding a shared
contract assembly, so the realistic mitigation is a test asserting the two sets agree, not a
refactor. Note too that `GetText` collapses `Pending Candidate Info` → `"Pending"` and
`Application Withdrawn` → `"Withdrawn"` for the badge while the dialog spells them out: two display
vocabularies for the same four values, on the same screen.

---

## 8. Sharp edges

### 8.1 The web timeline endpoint has no scope check (C1)

The design doc asserts one; there is none. `.RequireAuthorization()` proves only that *a* user is
signed in, and the handler passes the caller-supplied `EmailInvitationRequestId` straight to a
repository that filters on that column alone — no `ClientId`, no `RequestorId`, no
`IAtsAccessScopeResolver`.

Every neighbouring ATS read that takes a caller-supplied order id *does* scope: the public API's
`GetOrderAsync`, the Withdrawn page, and `ResendApplicationFormAsync` — which resolves
`IAtsAccessScopeResolver` and throws `NotFoundException` out of scope, with the design doc explicitly
warning *"Do not add a new entry point that bypasses that check."* This endpoint is that new entry
point, for reads.

What leaks is modest but real: subject-adjacent lifecycle timing (when the form was submitted, when
the report arrived, when it was disputed) plus the `Source` channel. No names, no report content.
Invitation ids are `Guid.CreateVersion7()`, so enumeration is impractical; the exposure is a user who
obtains an id from elsewhere — a shared screen, a URL, a support ticket. The fix is small: resolve
`IAtsAccessScopeResolver` in the handler and answer `NotFoundException` for an out-of-scope id,
matching the resend rule exactly.

### 8.2 There is no retention job for this table

ATS runs three pruning hosted services, all registered in `ServiceConfig/ATSServiceConfiguration.cs`:

| Service | Line | Target | Window | Interval |
|---|---|---|---|---|
| `AtsAuditRetentionService` | 85 | `AtsAuditEntry` | 30 days (`AtsAuditOptions.RetentionDays`) | 24 h |
| `AtsNotificationRetentionService` | 91 | `AtsNotification` | 30 days (`AtsNotificationOptions.RetentionDays`) | 24 h |
| `AtsEmailSendLogRetentionService` | 142 | `AtsEmailSendLog` | 48 hours (`AtsEmailDeliveryOptions.SendLogRetentionHours`) | — |

**None targets `ats.OrderStatusHistory`.** There is no `OrderHistoryRetentionService`, no options
class with a window for it, and no Quartz job that deletes from it. The table is append-only and
unbounded on a system where every enrolment writes at least one row and a bulk upload writes one row
*per subject* — a 10,000-row CSV adds 10,000 history rows in one `AddRangeAsync`.

This is arguably the correct design: unlike the audit trail (technical, 30-day) and the send log
(operational, 48-hour), a lifecycle timeline is something a requestor may legitimately ask about
years later, and pruning it would break §6.2's `WithdrawnAt` derivation for old orders. But it is an
*undocumented* decision — the design doc never mentions retention. If intentional, say so there; if
not, the three siblings are a working template, including the batched-delete loop and the
`Math.Max(1, ...)` option clamping they share.

### 8.3 The report re-upload branch records a completion that did not happen (C4)

`ReportService.UploadReportAsync` derives the status it writes to the order:

```csharp
		string orderStatus = OrderStatus.InProgress;
		DateTime? orderCompletedAt = null;
```

```csharp
			if (reportDetailsDTO.ReportStatus != ReportStatus.InitialReport)
			{
				orderStatus = OrderStatus.Completed;
				orderCompletedAt = DateTime.UtcNow;
			}
```

`UpdateOrderStatusAsync(..., orderStatus, orderCompletedAt, ...)` then sets the order row correctly
either way. But the two history branches disagree. The first-upload branch is guarded by
`added && orderStatus == OrderStatus.Completed && invitation.OrderStatus != OrderStatus.Completed`.
The re-upload branch (`ReportService.cs:107`) has **no guard**:

```csharp
				await _orderHistoryService.RecordAsync(
					invitation.EmailInvitationID,
					OrderHistoryEventType.ReportUploaded,
					invitation.OrderStatus,
					OrderStatus.Completed,
					cancellationToken);
```

Re-uploading an **initial** report therefore sets the order to `In Progress` while writing an entry
claiming it moved to `Completed` — and the dialog then tells the requestor "The final report was
uploaded and the order was completed." The order list and the timeline contradict each other. Copying
the first branch's guard onto the second is the fix.

### 8.4 A single-order background write cannot be attributed

`RecordAsync` has no `changedByUserId` parameter (§3.2); the only attributed overload is the bulk one.
Today exactly one background path writes history and it is genuinely bulk, so the gap is latent. It
becomes a trap the moment someone adds a Quartz job that transitions *one* order: the natural call is
`RecordAsync`, it compiles, and the entry is silently attributed to nobody. Either add the parameter
or document the one-element-`RecordManyAsync` workaround where the design doc tells job authors what
to do.

### 8.5 "Audit-like" overstates it

Three properties follow from §1.2: **cascade delete** means removing the order removes its timeline,
so the record does not outlive the thing it records; **`ChangedByUserId` has no FK and is never read**
(**C2**); and **nothing enforces immutability** — the convention is only that no code performs an
update. The real ATS audit trail is `AtsAuditEntry`, written by `AtsAuditService`, drained by
`AtsAuditDrainService`, pruned after 30 days. Different table, different purpose, different retention
— and ATS module 15 in the navigation is labelled "Audit Trail" and reads `AtsAuditEntry`, **not** this
table. Do not conflate the two when answering a compliance question.

### 8.6 `AtsAssistantPlugin` injects the service and never uses it

`AI/AtsAssistantPlugin.cs:50` declares `private readonly IOrderHistoryService _orderHistoryService;`
and line 69 assigns it. A grep of the file finds no other reference — no `RecordAsync`, no `GetAsync`
— yet both construction sites (`AtsAssistantService.cs:160` and `:404`) pass it in. Harmless but
misleading: it reads as though the assistant writes history directly, when in fact its order creation
is attributed indirectly through `InsertEmailInvitationRequestAsync`, with source `Web` rather than
anything assistant-specific.

### 8.7 No tests cover the factory or the service

There is **no test file** for `OrderHistoryFactory` or `OrderHistoryService`. All fourteen test
references to `IOrderHistoryService` are `Mock<IOrderHistoryService>` standing in for the real thing
(`ReportServiceTests`, `DisputeOrderServiceTests`, `OMSTicketingMonitoringServiceTests`,
`WithdrawnApplicationFilteringTests`, `AtsAssistantPluginTests`, `ATSServiceFixture`,
`DisputeOrderServiceIntegrationTests`), and two integration tests construct `OrderStatusHistory` rows
by hand to seed a read (`PublicApiRepositoryIntegrationTests.cs:132`,
`GetWithdrawnEmailInvitationRequestsIntegrationTests.cs:162`). The untested surface is therefore
precisely the part with the subtle rules — the `Guid.Empty → null` normalisation, the
`changedByUserId ?? _currentUser.UserId` precedence, the v7 id minting — in a class that is a pure
function of its arguments plus one injected dependency. About as cheap to test as anything in this
module, currently at zero coverage.

---

## 9. Wiring — what is registered where

`BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs:96-97`, inside `AddATSServices`,
immediately after the public API repository:

```csharp
		services.AddScoped<IPublicApiRepository, PublicApiRepository>();
		services.AddScoped<IOrderHistoryFactory, OrderHistoryFactory>();
		services.AddScoped<IOrderHistoryService, OrderHistoryService>();
```

Both are `Scoped`, which matters twice over: the factory captures `ICurrentUser`, which is
`IHttpContextAccessor`-backed and only meaningful inside a request scope. `IOrderHistoryRepository` is
not registered here at all — it is a slice of the decorated aggregate:

```csharp
	services.AddScoped<IATSRepository, ATSRepository>();
	services.Decorate<IATSRepository, ATSCacheRepository>();
```

reached through the standard `AddScoped<IXxxRepository>(provider => provider.GetRequiredService<IATSRepository>())`
forwarding. That is why `ATSCacheRepository` must implement the order-history members even though it
caches none of them: **adding a method to `IOrderHistoryRepository` is a compile error in
`ATSCacheRepository.OrderHistory.Cache.cs` until you add the pass-through too.**

There is no `AddHostedService` for this feature (§8.2), no keyed service, and no Quartz job. The Carter
module is found by assembly scan; the gateway route comes from `ATSPaths` implementing
`IReverseProxyModule`.

Four string literals must independently agree for §5 to work, with nothing enforcing any of them at
compile time — plus the query-parameter name `emailInvitationRequestId`, which must match the
endpoint's parameter name for minimal-API binding and appears only in the UI literal:

| Where | Literal |
|---|---|
| `GetOrderStatusHistoryEndpoint.cs:10` | `"getorderstatushistory"` |
| `Path/ATSPaths.cs:11` | `"/ats/getorderstatushistory"` |
| `Path/ATSPaths.cs:16` | `"/getorderstatushistory"` |
| `UI/.../Services/ATS/Report/ReportService.cs:250` | `$"ats/getorderstatushistory?emailInvitationRequestId={...}"` |

---

## 10. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| An `OrderHistoryEventType` constant | All four switches in `OrderStatusHistoryDialog.razor` (`GetTitle`, `GetDescription`, `GetTone`, `GetIcon`) | Hand-typed literals in a different assembly; the fallbacks are silent, so a rename renders raw (§7.2) |
| An `OrderHistoryEventType` constant | `ATSRepository.WithdrawnApplications.cs:44` | It matches `ApplicationFormWithdrawn` to derive the screen's `WithdrawnAt`; a rename blanks the column with no compile error (§6.2) |
| An `OrderStatus` constant | `UI/FrontendWebassembly/SharedService/OrderStatusDisplay.cs:6-9` | A hand-synced copy across an assembly boundary, linked only by a comment; `OrderStatus` is `internal` so it cannot be shared (§7.3) |
| An `OrderStatus` string value | Existing rows in `ats.OrderStatusHistory` | Statuses are stored as display strings with spaces; a rename is a data migration, not a refactor (§2.3) |
| `OrderStatusHistoryDTO` (backend record) | `UI/FrontendWebassembly/DTO/ATS/OrderStatusHistoryDTO.cs`, `PublicApiRepository.cs:54`, `ATSRepository.OrderHistory.cs:28` | Three positional-constructor call sites plus a name-bound UI mirror; reordering is a silent mis-bind, not a compile error |
| `IOrderHistoryRepository`'s members | `ATSCacheRepository.OrderHistory.Cache.cs` | The cache decorator must implement every member even though it caches none of them (§9) |
| The `(EmailInvitationRequestId, OccurredAt)` index | `ATSRepository.OrderHistory.cs:25` and `ATSRepository.WithdrawnApplications.cs:42` | Both reads filter by invitation and sort/filter by time; there is no index on `EventType` (§1.2) |
| `DeleteBehavior.Cascade` on the FK | Any future retention or compliance requirement | Deleting an order currently deletes its timeline — the table is not independently durable (§8.5) |
| `RecordAsync`'s signature | All twelve call sites in §4 | Twelve callers across seven files; six rely on the `source` default of `Web` |
| `RecordManyAsync`'s single status pair | Sites 3, 10, 12 | It cannot vary previous/new per order, which is why the bulk paths write `null` or `""` where their single-order twins write the real value (§4.3) |
| Anything about who is recorded | `OrderStatusHistoryDTO` | `ChangedByUserId` is written but never projected, so no consumer sees it (**C2**, §3.3) |
| The `GetOrderStatusHistory` route string | `ATSPaths.cs:11`, `ATSPaths.cs:16`, `UI/.../Report/ReportService.cs:250` | Four independent literals in three assemblies, plus the query-parameter name (§9) |
| `ReportService`'s `orderStatus` derivation | `ReportService.cs:107` | The re-upload branch records `Completed` with no guard, unlike the first-upload branch at `:141` (§8.3) |
| `RequeueEmailInvitationAsync`'s `OrderStatus` write | `EndorsementSubmissionService.cs:523-531` | That call site's comment already contradicts its code; the code is right only because the repository really does reset the status (§4.3) |
| A new background job that writes history | `IOrderHistoryService.RecordAsync`'s missing `changedByUserId` | The single-order overload cannot attribute; you must use `RecordManyAsync` with one element (§8.4) |
| Retention for this table | The three sibling `*RetentionService` classes and `ATSServiceConfiguration.cs:85/91/142` | There is no pruning for `OrderStatusHistory` today; the siblings are the template if that changes (§8.2) |
