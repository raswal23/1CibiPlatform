# OMS Auto-Ticketing — Implementation Review

Raises an OMS ticket automatically for every ATS order, writes the returned ticket
number back onto the order, and adds a **Ticketing Status** screen that mirrors Bulk
Uploads Status.

Branch: `feature/OMS-Generic-API-Integration`. Follows `docs/feature-development-guide.md`.

> **Reconciled against the code.** This document originally described the feature as first
> designed; several things changed afterwards. The package join moved from name to id,
> `TurnAroundTimeID` stopped being a constant, a bulk retry slice was added, the slices moved
> under `Features/Web/`, and the failure catch is broader than first written. Those are
> corrected in place below. For current call chains, exact signatures and verbatim code, read
> [`oms-auto-ticketing_code_explanation.md`](oms-auto-ticketing_code_explanation.md) — its §0
> tables every correction.

---

## 1. What was decided, and why

### Trigger point: enrolment

The ticket is queued the moment the `EmailInvitationRequest` row is inserted.

The consequence, which is worth being explicit about: candidate identity is captured on the
order only for data-screening orders, where no application form is ever sent. On a manual
order the ticket carries `DateOfBirth = null` and blank `SSSIDNumber` / `TIN`.

That is safe against the OMS contract:

- `CreateTicketCommandValidator` applies its 10-digit SSS and 9–12-digit TIN rules only
  `.When(...)` the value is non-empty.
- `OMSRepository` maps null → `DBNull` for `@p_birthdate` and → `string.Empty` for the
  two id parameters.
- The phone number is always present, because enrolment validates `MobileNumber` as
  11 digits.

If an order is ticketed *after* the form is submitted (a retry, say), the mapper
automatically prefers the richer `PersonalDetails` values. Nothing needs to change to
get that behaviour.

### No new table

The original sketch proposed copying rows into a staging table so a claimed row would
not be "blocked" for other users. That turned out to be unnecessary: in the existing
bulk pattern the `FOR UPDATE SKIP LOCKED` sits on an inner sub-SELECT, so the lock lives
only for the duration of that one `UPDATE` statement. The **durable** claim is the
status-column write. Ordinary reads — dashboards, lists, the projection job — are never
blocked.

So ticket state is seven columns on `EmailInvitationRequest`, exactly like the existing
`EmailSentStatus` / `EmailClaimedAt` / `EmailSendAttempts` trio. No copy, no second write
path, nothing to drift, and the UI reads status straight off the order.

### Quartz job, not `BackgroundService`

Matching the bulk orders pattern. Quartz is already registered with a persistent,
**clustered** Postgres store (`ats.qrtz_*`, `UseClustering()`), so this is safe across
API instances: `[DisallowConcurrentExecution]` guards within a node, `SKIP LOCKED`
guards across nodes. (The repo's only real `BackgroundService` is
`PlatformLogRetentionService`, which is unrelated.)

---

## 2. Step by step

### Step 1 — Ticket state on the order

`Data/Entities/EmailInvitationRequest.cs` plus its fluent configuration in
`Data/EntityConfiguration/EmailInvitationRequestConfiguration.cs` — the entity is a plain
POCO with no mapping attributes, and the configuration is picked up by
`ApplyConfigurationsFromAssembly` in `ATSDBContext`:

| Column | Type | Purpose |
|---|---|---|
| `TicketStatus` | `varchar(50)` | `Pending` / `Processing` / `Done` / `Error` |
| `IsTicketed` | `bool`, default false | terminal flag — false is claimable, true never again |
| `TicketNumber` | `varchar(100)` | from `OMSTicketCreated.TicketNumber` |
| `TicketDeliveryDate` | `timestamptz` | from `OMSTicketCreated.DeliveryDate` |
| `TicketClaimedAt` | `timestamptz` | stale-claim sweeper input |
| `TicketAttempts` | `int`, default 0 | retry budget |
| `TicketError` | `varchar(500)` | last failure reason, shown in the UI |

Plus one index on `TicketStatus`, mirroring the `EmailSentStatus` index that drives the
email claim query. Only one — the config file warns this table is write-hot.

New `Constants/TicketStatus.cs`, `public` (unlike the `internal` `EmailStatus`) so the
API slice validators can share the vocabulary.

**Note on the column name:** a `TicketStatus` column existed once before, but migration
`20260713052049` renamed it to `OrderStatus`. No column by that name exists today, so the
name is free and the new `AddColumn` is safe on existing databases. Be aware that the
*original* `TicketStatus` (added by `20260710065948`) was `varchar(255) NOT NULL DEFAULT ''`
— a different type and nullability from today's `varchar(50) NULL`. Anyone reading old
migrations will find a `TicketStatus` that meant the order lifecycle, which is now
`OrderStatus`. The two are unrelated.

**Note on nullability:** unlike its email sibling (`EmailSentStatus` is
`.IsRequired(true).HasMaxLength(255)`), `TicketStatus` is `.IsRequired(false)`. That single
choice is why every order created before this feature has `TicketStatus = NULL` and is
therefore neither queued nor listed — see §4 item 6. Seed rows in `ATSInitialData` do not
set it either.

Migration: `BackendAPI/API/APIs/Migrations/ATS/20260826103338_AddOMSTicketingColumnsATSMigration.cs`
— additive only (seven `AddColumn` calls plus one index), no destructive change. Migrations
are namespaced per module under `Migrations/ATS/`.

### Step 2 — Requestor identity

`ICurrentUser` is `IHttpContextAccessor`-backed, so it resolves to **null on a Quartz
thread**. The codebase already learned this once (see the comment in
`EndorsementSubmissionService.InsertBulkSubjectAsync`).

As requested, `ICurrentUser` gained `FirstName` / `MiddleName` / `LastName`:

- `Auth/Constants/AuthClaimTypes.cs` — three new claim names.
- `Auth/Services/Login/JWTService.cs` — emits them. `LoginDTO` already carried all three
  name parts, so this is purely additive; **tokens issued before this change return null
  for them until the user signs in again.**
- `Auth/Shared/Implementations/CurrentUser.cs` — reads them. **The precedence is the
  standard claims first, the new ones as fallback:**
  `GetClaimValue(ClaimTypes.GivenName, AuthClaimTypes.FirstName)`. This is invisible today
  because `JWTService` never emits `GivenName` / `Surname` (its only standard claim is
  `NameIdentifier`), so the first lookup always misses. It would matter for a token issued
  by another party — notably the **SAML2 SSO** path, where an external IdP commonly does
  emit them and would then win over the platform's own stored name parts. `MiddleName` has
  no standard-claim fallback at all. Worth a deliberate decision; see §4 item 5.

The ticketing job itself does **not** use `ICurrentUser`. It resolves the requestor from
persisted data:

- `Site` ← `ATS.UserDetails.Site`, looked up by `RequestorId`. That table's PK is
  composite `(UserId, ModuleId)` — one row per module grant, each carrying the same
  `Site` — so the query takes one row rather than assuming uniqueness.
- requestor first/last name ← `IAuthQueries.GetATSAssignedUserAsync`, the existing
  sanctioned cross-module lookup. `EmailInvitationRequest.Requestor` holds only a joined
  display name, which cannot be split back apart safely.

### Step 3 — ATS → OMS wiring

`ATS.csproj` gained a project reference to `OMS.csproj` (there was none;
`IOMSTicketCreator` had no consumer outside its own module and tests).

`IOMSTicketCreator.CreateTicketAsync` gained an optional
`string referenceNumber = ""` parameter, passed through to the stored procedure's
`@p_reference_no` — which `OMSRepository` already bound but always received as empty.
The job sends the `EmailInvitationID`, so an OMS ticket can be traced back to its ATS
order and a retry-after-timeout is recognisable. Existing callers are unaffected by the
default.

### Step 4 — Queue the order at enrolment

Two lines at each insert point, setting `TicketStatus = Pending` and `IsTicketed = false`:

- `EndorsementSubmissionService.InsertEmailInvitationRequestAsync` — reached by **three**
  callers, not two:
  | Caller | Path | `source` |
  |---|---|---|
  | `InsertEmailInvitationRequestHandler` (web console) | `Features/Web/InsertEmailInvitationRequest/` | defaults to `OrderHistorySource.Web` |
  | `CreateEndorsementHandler` (**public API**) | `Features/PublicApi/CreateEndorsement/` | `OrderHistorySource.PublicApi` |
  | `AtsAssistantService.ConfirmOrderDraftAsync` (AI assistant) | `Services/AIAssistant/` | defaults to `Web` |
- `BulkSubmissionProcessorService` (bulk CSV rows)

Both write inside the same transaction as the order itself
(`TransactionRunner.RunAsync(_unitOfWork, ...)`), so there is no window where an order
exists unqueued. The bulk path runs on a Quartz thread with no `HttpContext`, so its
`ClientId` / `RequestorId` / `Requestor` come from the `BulkUploadFileDetails` row rather
than `ICurrentUser`.

These are the **only two** write-side assignments in the repository. In particular
`ResendApplicationFormAsync` / `ResendApplicationFormsAsync` do **not** touch ticket state —
a resent order keeps whatever ticket status it already had.

There is no outbox and no domain events in this repo — the status column *is* the queue,
exactly as `NeedsProjection` is for the projection job.

### Step 5 — Repository

`Data/Repository/OMSTicketing/` — a focused repository, **not** part of `IATSRepository`
and deliberately **not** behind the `ATSCacheRepository` decorator. Same reasoning as
`BulkUploadRepository`: a Pending order becomes Done within one 10-second tick, so a
cached page would show precisely the staleness the screen exists to remove.

The claim query copies the email queue's shape verbatim:

```sql
WITH ranked AS (
    SELECT "EmailInvitationID",
           ROW_NUMBER() OVER (PARTITION BY "ClientId" ORDER BY "OrderCreatedAt") AS rn
    FROM ats."EmailInvitationRequest"
    WHERE "IsTicketed" = false
      AND ("TicketStatus" = {2} OR ("TicketStatus" = {3} AND "TicketAttempts" < {4}))
)
UPDATE ats."EmailInvitationRequest" t
SET "TicketStatus" = {0}, "TicketClaimedAt" = {1}
WHERE t."EmailInvitationID" IN (
    SELECT e."EmailInvitationID" FROM ats."EmailInvitationRequest" e
    WHERE e."EmailInvitationID" IN (SELECT "EmailInvitationID" FROM ranked WHERE rn <= {5})
    ORDER BY e."OrderCreatedAt" LIMIT {6} FOR UPDATE SKIP LOCKED)
RETURNING t.*;
```

Throughput knobs. They are **not all in one file** — each lives next to the code that owns
the decision:

| Knob | Value | Declared in | Why |
|---|---|---|---|
| `ClaimBatchSize` | 50 | `OMSTicketingRepository` (`private`) | each ticket is 3 stored-procedure round trips to a remote legacy SQL Server, so smaller than the email queue's 100 |
| `PerClientSliceSize` | 30 | `OMSTicketingRepository` (`private`) | one large bulk upload cannot starve other clients |
| `MaxTicketAttempts` | 5 | `OMSTicketingRepository` (**`public`**) | poison-message guard, same as `MaxEmailSendAttempts`. Public so the UI can print `5/5` |
| `MaxDegreeOfParallelism` | 3 | `OMSTicketingProcessorService` | concurrent OMS calls |
| `StaleClaimTimeout` | 30 min | `OMSTicketingProcessorService` | passed *into* `ReleaseStaleTicketClaimsAsync`; must exceed the worst-case batch duration |
| tick interval | 10s | `OMSTicketingBackgroundJobSetup` | same as the bulk and email jobs |

`MarkTicketFailedAsync` takes an `isRetryable` flag. A **non**-retryable failure sets
`TicketAttempts` straight to the cap rather than incrementing, so a condition that cannot
resolve itself is not re-attempted five times over 50 seconds. It also truncates the reason
to 500 characters to match the `TicketError` column — without that, a long OMS exception
message would throw on write and mask the original failure.

`GetTicketPayloadsAsync` left-joins `PersonalDetails`, `PackageDetails` and `UserDetails` in
one round trip. Left joins are deliberate: a missing row must still come back so the service
can park it *with a reason* instead of silently dropping it from the batch. `UserDetails`
needs `.Take(1)` because its PK is composite `(UserId, ModuleId)`.

**`PackageDetails` is joined by `PackageId`, not by name.** It was originally matched by name;
that was changed because renaming a package silently orphaned every order referencing it — they
kept the old string and parked as an error nobody could explain. The repository comment says so
explicitly, and `20260830124343_AddPackageIdToOrdersATSMigration` postdates the ticketing
migration, which is when the change landed. Only the `ReportTypeID` parse still reads free text
(see Step 6).

### Step 6 — Payload mapping

`Services/OMSTicketing/OMSTicketPayloadMapper.cs` — pure and static, so the rules are
testable without a database or a live OMS connection.

| OMS field | Source |
|---|---|
| `FirstName` / `MiddleName` / `LastName` | `EmailInvitationRequest` (`MiddleInitial` → `MiddleName`) |
| `DateOfBirth` | `EmailInvitationRequest.DateOfBirth`, null unless data screening |
| `EmailAddress` | `EmailInvitationRequest.EmailAddress` |
| `PhoneNumber` | `EmailInvitationRequest.MobileNumber`, normalised |
| `SSSIDNumber` / `TIN` | `EmailInvitationRequest.SSSNumber` / `TINNumber`, blank unless data screening |
| `Remarks` | literal `"Remarks"` |
| requestor name / email | Auth directory lookup |
| `Site` | `UserDetails.Site` |
| `TurnAroundTimeID` | **derived**, not a constant — `TryResolveTurnAroundTimeId(payload.RushNormal)`: Normal → `1`, Rush → `2`. An unrecognised value parks the order, so a new order type surfaces as a visible `Error` instead of silently going out as Normal |
| `ReportTypeID` | parsed from `PackageDetails.PackageDescription` |
| `CountryID` / `ProvinceID` / `CityID` | `0` |
| `Address` / `PostalCode` | `""` |

Two normalisations worth reviewing:

- **Phone** — strips spaces and dashes, converts `+63…` / `63…` to the local `0…` form,
  and adds a missing trunk zero to a bare `9…` number. Returns null if the result is not
  11–12 digits, which parks the order rather than letting OMS reject it.
- **SSS / TIN** — kept only when the digit count is accepted (SSS exactly 10, TIN 9 to 12);
  otherwise sent blank. The field is optional, so a malformed value must not fail the whole
  ticket. TIN takes a range because 9 digits (individual) and 12 (with branch code) are both
  issued — an exact-12 rule here silently blanked every 9-digit TIN on its way to OMS.

**How a mapping failure is signalled:** `TryMap` returns a tuple, not an exception and not a
bare null — `(CreateOMSTicketRequest? Request, string? Failure)`. On any un-mappable input it
returns `(null, "<reason>")`, and the processor parks the order with that reason. The class doc
states the invariant: *"A failure here is never retryable: none of these inputs change on their
own."*

**`ReportTypeID` is the fragile part of this feature and deserves attention in review.** The
package row is now located by `PackageId` (Step 5), but the numeric report type is still stored
in `PackageDetails.PackageDescription`, a 500-character free-text column. The mapper takes the
**leading digit run** of that description, so `"182"`, `" 182 "` and `"182 - Criminal Records
Check"` all yield `182`. If the package row is missing or the description does not yield a
positive integer, the order is parked as `Error` with the reason and **OMS is never called** —
no wasted PO validation, and it is visible for someone to fix the package configuration.

A vestige of the old name-matching survives in the mapper's *error messages*, which still
interpolate the free-text name: `$"No active package matches \"{payload.SelectPackage}\", so the
OMS report type is unknown."` The message can therefore name a package that was in fact found by
id. Cosmetic, but confusing when reading a parked order's `TicketError`.

### Step 7 — Worker and job

`Services/OMSTicketing/OMSTicketingProcessorService.cs`, modelled on
`BulkSubmissionProcessorService`:

1. Sweep stale claims back to Pending (`StaleClaimTimeout = 30 min`, which must exceed
   the worst-case batch duration or the sweeper would steal rows from a live worker).
2. Claim a batch atomically.
3. Load payloads for the claimed ids; park any claimed id whose payload could not be
   loaded, so it does not sit in `Processing` until the sweeper releases it.
4. Process with `SemaphoreSlim(3)`, each order in its **own DI scope** — the repository
   owns a `DbContext`, which is not safe to share across concurrent calls.
5. Per-order try/catch, so one bad order cannot poison the batch.

Failure classification — only two exception types are special-cased:

- `BadRequestException` from OMS (invalid requestor, exhausted PO) → **not** retryable.
  Retrying cannot fix either; it needs a human.
- `OperationCanceledException` → neither; shutdown is not a failure.
- **Everything else** → retryable, consumes one attempt. The catch is
  `catch (Exception ex) when (ex is not OperationCanceledException)`, which is broader than
  "InternalServerException / connectivity".
- Mapping failure → not retryable, and no OMS call at all.

> ⚠️ **Consequence of the broad catch, worth a decision.** `OMSSqlConnectionFactory` throws a
> plain `InvalidOperationException` when `OMS_Connection` is missing or still an unresolved
> `${...}` placeholder — deliberately lazy so the Testing environment can boot without an OMS
> secret. That exception is not special-cased, so a **configuration** error is classified as
> transient: every order burns all five attempts over ~50 seconds and parks as `Error` with a
> config message in `TicketError`. Recoverable via Retry once configured, but a single missing
> secret presents as thousands of business-data errors. If this ever bites, classify that
> exception as non-retryable, or fail the batch before claiming rather than per order.

Two behaviours not described in the original design:

- **Exhausted-ticket notification.** Every failure path ends with
  `NotifyIfTicketingExhaustedAsync`, which asks `GetExhaustedTicketIdsAsync` and — only when the
  id is genuinely exhausted — raises `AtsNotificationType.TicketingFailed`. The park is therefore
  *not* silent: the requestor is told exactly once, at the moment the automatic budget runs out,
  which is also when the manual Retry button appears (Step 11). Checking exhaustion through the
  repository rather than inferring it from the attempt count is what makes it fire once instead
  of on every subsequent pass.
- **Cache revocation.** After a batch in which anything succeeded, the processor revokes the
  HybridCache tags `Report`, `DisputeOrder` and `WithdrawnApplication` — a new ticket changes
  what those cached pages show, so they are dropped rather than left to expire.

`BackgroundJobs/OMSTicketing/` holds the thin Quartz shell plus its trigger setup,
registered via `services.ConfigureOptions<OMSTicketingBackgroundJobSetup>()`.

### Step 8 — API slices and gateway

Two query slices under `Features/Web/OMSTicketing/Query/` — note the **`Web/`** level, since
ATS splits `Features/` by trust boundary — one folder per operation as the guide requires:

| Slice | Carter route | Gateway route |
|---|---|---|
| `GetTicketedOrders` | `getticketedorders` | `/ats/getticketedorders` |
| `GetTicketStatusCounts` | `getticketstatuscounts` | `/ats/getticketstatuscounts` |

The two retry commands from Step 11 live beside them under `Features/Web/OMSTicketing/Command/`,
so all four routes are registered together in `Path/ATSPaths.cs` — the typed module is the only
source of gateway routes at runtime. Verify with `GET /__routes`.

Unlike the route immediately preceding them in `ATSPaths.cs`, **these four carry no
`RateLimitPolicy` metadata**, so they fall through to the gateway's 500/s default. Acceptable for
a staff console behind authentication; it would not be for a machine-facing route.

Read logic lives in `Services/OMSTicketingMonitoring/`, kept separate from the write-side
processor, mirroring `BulkUploadMonitoring` vs `BulkSubmissionProcessor`. It scopes every
read through `IAtsAccessScopeResolver`, and — like every other ATS list — a caller outside
the role ladder gets an empty list rather than a 403. Counts honour the search and date
filters but never the selected status, so each chip keeps reporting its own size.

### Step 9 — UI

`Component/ATS/OMSTicketing/TicketingStatusComponent.*`, at `/s&i/ats/ticketingstatus`.
Same shape as Bulk Uploads Status: intro banner, segmented status filter with live
counts, keyset-paginated table with search, date range and reload.

Buckets are `All | Pending | Processing | Done | Error`. Bulk has three; **Error** is
added here so a failed ticket is visible rather than silently absent, with the reason
shown beneath the pill (clamped to two lines, full text on hover).

Columns: Subject (name + relative date), Requestor, Package, Ticket No. (monospaced —
it is the one value a user copies out of this screen), Delivery Date, Status — plus the
`Select` checkbox column for bulk retry and the `Action` column added in Step 11. The table
declares **`ColumnCount="8"`**; all eight must be accounted for when adding or removing one.

Registered as ATS module **14** (`ticketingstatus`, "Ticketing Status") in **four** places that
must agree:

1. `AtsModuleIds.TicketingStatus` (backend constant)
2. the UI `ModuleList` entry — whose `path` string must equal the `@page` route's last segment
3. the seed data in `ATSInitialData`
4. **`ModuleList.IsPrimaryNavigationModule`** — the predicate deciding sidebar vs. "Manage". It
   currently reads `moduleId <= 5 || moduleId == 12 || moduleId == 13 || moduleId == 14`. Add a
   module 16 and forget this line, and the page is reachable by URL but renders in no navigation.

Because `ATSDatabaseExtensions` only seeds modules into an empty table, the existing
`BackfillBulkUploadsModuleAsync` was generalised to
`BackfillModuleGrantedWithNewOrderAsync(moduleId)` and is now called for both modules —
so existing databases get module 14 granted to everyone who already has New Order. It is
idempotent, so it is safe on every startup. **Skipping this step is the classic failure:** the
screen works on a freshly seeded dev database and is invisible in production.

### Step 10 — CSS: shared, not copied

The first cut of this screen duplicated ~300 lines of the bulk stylesheet with renamed
classes. That was reworked, because two copies drift and a design fix would then have to
be made twice.

What changed:

- The bulk-specific toolbar rules in `wwwroot/css/ats.css` were **renamed** to neutral
  `.ats-status-board-*` classes, and the shared board shape (intro banner, filter chips,
  status dots, status pills, lead-identity cell, tag and muted cells) now lives there
  once, under `.ats-management-page` and its `--management-*` palette.
- **Bulk Uploads was migrated onto those shared classes in the same change** — its scoped
  stylesheet went from ~360 lines to 95, keeping only what is genuinely its own (the
  email progress bar, order-type chip, subject count).
- Ticketing's scoped stylesheet is 55 lines: the monospaced ticket number, the stacked
  status cell, the attempt count, and the clamped error text (plus one 720px media query).
  Its header comment forbids re-declaring any shared rule, so the constraint survives the
  next person editing it.
- Unused hooks (`bulk-uploads-scope` wrapper, two `TableClass` values matching no rule)
  were removed.

The rule behind this was written into `docs/feature-development-guide.md` under
**"Reuse existing styles; do not duplicate a design"**, with a matching entry in the
definition-of-done checklist.

---

### Step 11 — Manual retry for exhausted orders

Auto-retry gives up after 5 attempts and parks the order as `Error`. That park was
terminal: the claim query's `"TicketAttempts" < 5` clause meant the job would never look
at the row again. But the causes are usually fixable — a package description corrected, a
PO topped up, a Site assigned, OMS back after an outage — so exhausted rows now get a
**Retry** button.

**When it appears:** `TicketStatus = Error` **and** `TicketAttempts >= 5`. Rows still
auto-retrying show a muted `—`, since the job will reach them on its own tick. Orders
parked immediately (unresolvable package, missing Site, OMS `BadRequestException`) qualify
too, because `MarkTicketFailedAsync(isRetryable: false)` writes them straight at the cap.

**What it does:** resets `TicketStatus = Pending`, `TicketAttempts = 0`,
`TicketError = null`, so the job treats the order as fresh and gives it a full 5
automatic attempts before parking again.

`OMSTicketingRepository.RequeueExhaustedTicketAsync` — the `WHERE` clause is the
concurrency guard and carries the correctness of this feature:

```csharp
.Where(x => x.EmailInvitationID == emailInvitationId
         && !x.IsTicketed
         && x.TicketStatus == TicketStatus.Error
         && x.TicketAttempts >= MaxTicketAttempts)
```

Matching the exhausted state *inside* the `UPDATE` rather than reading first means a row
the job has already re-claimed, or that a second operator retried a moment earlier,
updates 0 rows and the caller is told. `!IsTicketed` is the guard against requeuing a
ticketed order and raising a duplicate in OMS.

`OMSTicketingMonitoringService.RetryTicketAsync` runs the scope ladder, but it produces
**three** distinct outcomes — the original "out of scope reads as 404, not 403" wording
conflated the first two:

| Condition | Exception | Status | Why |
|---|---|---|---|
| Caller has no ATS access at all | `ForbiddenException` | **403** | Nothing about any order is disclosed — the caller simply is not an ATS user |
| Order unknown **or** outside the caller's client/owner scope | `NotFoundException` | **404** | A 403 here would confirm that another client's order exists |
| Order no longer in the exhausted state | `ConflictException` | **409** | Stale button: the job re-claimed it, or another operator won the race |

Note the read path behaves differently by design: a caller with no scope gets an **empty list**,
not a 403 (§Step 8). Reads return empty, writes throw.

The action is recorded to the existing `ats."OrderStatusHistory"` as
`TicketRetryRequested`, stamped with the acting user by `OrderHistoryFactory`. The order's
own `OrderStatus` is written unchanged on both sides — a ticket retry is not a step in the
order lifecycle. This service is HTTP-scoped, so `ICurrentUser` resolves normally here,
unlike the Quartz job.

API: `PATCH retryticket` → gateway `/ats/retryticket`, one slice under
`Features/Web/OMSTicketing/Command/RetryTicket/`, copied from `Features/Web/ResendApplicationForm/`.

UI: an `Action` column, a `YesNoDialogComponent` confirm with warning styling matching
the withdrawn-list resend, a `_retryingOrderId` guard so a double-click cannot queue twice,
and the attempt count (`5/5`) shown inside the Error pill so it is visible *why* the button
appeared. The button reuses a new shared `.ats-cell-action` class in `ats.css` rather than
a screen-specific copy. The client-side guard and the server-side `WHERE` clause are **both**
needed: the UI stops one browser double-clicking, the SQL stops two browsers — or a browser
racing the job.

### Step 12 — Bulk retry

Added after the original design, and the reason Step 9's table has a `Select` column.

`PATCH retrytickets` → gateway `/ats/retrytickets`, slice at
`Features/Web/OMSTicketing/Command/RetryTickets/`. Request is a collection of ids; response is
`(RequestedCount, RequeuedCount, IsComplete)`.

- **Capped at 500** (`OMSTicketingMonitoringService.MaxBulkRetrySize`), enforced in *both* the
  validator and the service. The reasoning is in the constant's comment: each requeued order
  becomes an OMS round trip on the job's next passes, so releasing thousands at once would
  monopolise the job and block every other client behind one operator's click.
- **No 409.** Unlike the single retry, a stale selection is not an error — it is reported in the
  counts. The UI shows `"{RequeuedCount} of {RequestedCount} order(s) queued. The rest were
  already back in the queue."` at `Severity.Info`, not `Error`.
- `RequeueExhaustedTicketsAsync` uses **the same `WHERE` predicate** as the singular version and
  returns the count of rows that actually moved, which is what makes the honest count possible.
- History is written once for the **eligible** ids only, via `RecordManyAsync`, with no per-row
  previous status.
- Selection is deliberately **page-scoped and pruned on every reload**
  (`_selectedInvitationIds.RemoveWhere(id => !stillSelectable.Contains(id))`) — the UI-side
  counterpart to the server's per-row scope filtering. A selection can never outlive the page it
  was made on.
- Guarded by a separate `_isBulkRetrying` flag, since `_retryingOrderId` is per-row.

## 3. Tests

| Suite | Result |
|---|---|
| `OMSTicketPayloadMapperTests` | 29 passed |
| `OMSTicketingProcessorServiceTests` | 9 passed |
| `OMSTicketingMonitoringServiceTests` (retry) | 8 passed |
| `OMSTicketingRepositoryIntegrationTests` (real Postgres) | 17 passed |
| Full ATS suite (unit + integration) | 374 passed |
| `Auth.UnitTests` (JWT claims changed) | 99 passed |
| `dotnet build 1CibiPlatform.sln` | succeeded |

The processor tests pin the behaviour that is easy to regress: the reference number is
the invitation id; a mapping failure parks **without** calling OMS; a `BadRequestException`
is not retryable while an `InternalServerException` is; one failing order does not stop
its siblings; and a claimed order with no payload is parked rather than left stranded.

The integration tests run against a real Postgres Testcontainer, which is the only place
the Npgsql date-kind bug and the `SKIP LOCKED` claim actually reproduce. The load-bearing
one for manual retry asserts that a requeued order is picked up by the **real claim SQL**
on the next pass — not merely that its columns changed. Others cover the refusals: still
auto-retrying, already ticketed, not parked, and a double-click where only the first call
wins.

> `dotnet format` initially rewrote trailing whitespace in ~70 unrelated Auth and ATS
> files. Those were reverted, so the diff is scoped to this feature.

---

## 4. Not done / worth a decision

1. **Not verified against a live OMS.** Everything is tested against a faked
   `IOMSTicketCreator`. The end-to-end check against the OMS UAT database — enrol an
   order, wait a tick, confirm a real `ticket_no` and that the ticket carries the
   `EmailInvitationID` as its reference — still needs to be run.
2. **Claim-under-real-concurrency is still unproven.** `OMSTicketingRepositoryIntegrationTests`
   now exercises the real claim SQL against Postgres and asserts an order is claimed
   exactly once across two sequential passes. What is *not* tested is two workers racing
   the same batch simultaneously — that would need parallel `ProcessAsync` calls on
   separate connections to prove `SKIP LOCKED` end to end.
3. **Throughput knobs are constants, not configuration**, and they are spread across three
   files (`OMSTicketingRepository`, `OMSTicketingProcessorService`,
   `OMSTicketingBackgroundJobSetup`) rather than sitting together. This matches the bulk and
   email jobs, which also hardcode theirs. Easy to move to `appsettings` if you want them tuned
   without a deploy — and worth centralising at the same time, since `StaleClaimTimeout` must
   stay larger than the batch duration implied by the other two.
4. **`ReportTypeID` depends on a free-text convention.** Storing the numeric id in
   `PackageDescription` works, but nothing enforces it — editing a package's *description* so it
   no longer starts with digits silently breaks ticketing for every order using it (they park as
   `Error`, visibly). A real `ReportTypeId` column on `PackageDetails` would remove the
   guesswork. Note this is narrower than it used to be: since the join moved to `PackageId`,
   *renaming* a package no longer orphans anything.
5. **Existing tokens lack the new name claims** until users sign in again, so
   `ICurrentUser.FirstName` / `LastName` read null for them. The ticketing job does not
   depend on this — it reads the Auth directory — but any other new consumer would.
6. **Orders created before this change have `TicketStatus = null`** and are therefore not
   queued and not shown on the new screen. If they should be back-filled and ticketed,
   that is a deliberate one-off `UPDATE` and should be a separate decision.
7. **A missing `OMS_Connection` is classified as retryable.** See the warning in Step 7:
   `OMSSqlConnectionFactory` throws a plain `InvalidOperationException`, which the processor's
   broad catch treats as transient, so a configuration error consumes every order's retry budget
   and parks them all as business-data errors. Decide whether to classify it as non-retryable or
   to fail the batch before claiming.
8. **`CurrentUser`'s claim precedence may be backwards.** `GivenName` / `Surname` are tried
   *before* the platform's own `firstName` / `lastName` claims (Step 2). Harmless while
   `JWTService` is the only issuer, but the SAML2 SSO path can introduce a token that does carry
   the standard claims, and those would then win. `MiddleName` has no fallback either way.
9. **The status vocabulary is duplicated into the UI by hand.** `OrderTicketStatus` in
   `UI/FrontendWebassembly/DTO/ATS/OMSTicketingDTO.cs` mirrors `ATS.Constants.TicketStatus`, and
   its `MaxAttempts = 5` mirrors `OMSTicketingRepository.MaxTicketAttempts`. The WASM project does
   not reference the ATS assembly, so **nothing at compile time keeps them in sync** — rename a
   status on the backend and the UI's filter chip silently returns zero rows. A shared contracts
   package, or a generated file, would close this.
