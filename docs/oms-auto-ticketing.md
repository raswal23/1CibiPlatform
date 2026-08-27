# OMS Auto-Ticketing — Implementation Review

Raises an OMS ticket automatically for every ATS order, writes the returned ticket
number back onto the order, and adds a **Ticketing Status** screen that mirrors Bulk
Uploads Status.

Branch: `feature/OMS-Generic-API-Integration`. Follows `docs/feature-development-guide.md`.

---

## 1. What was decided, and why

### Trigger point: enrolment

The ticket is queued the moment the `EmailInvitationRequest` row is inserted.

The consequence, which is worth being explicit about: `PersonalDetails` does **not**
exist at that point — it is only written when the applicant submits the application form
(`ApplicationFormService.cs`, `AddPersonalDetailsDataAsync`). So the first ticket carries
`DateOfBirth = null` and blank `SSSIDNumber` / `TIN`.

That is safe against the OMS contract:

- `CreateTicketCommandValidator` applies its 10-digit SSS and 12-digit TIN rules only
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

`Data/Entities/EmailInvitationRequest.cs` + its entity configuration:

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
`20260713052049` renamed it to `OrderStatus`. No column by that name exists today, so
the name is free and the new `AddColumn` is safe on existing databases.

Migration: `20260826103338_AddOMSTicketingColumnsATSMigration` — additive only, no
destructive change.

### Step 2 — Requestor identity

`ICurrentUser` is `IHttpContextAccessor`-backed, so it resolves to **null on a Quartz
thread**. The codebase already learned this once (see the comment in
`EndorsementSubmissionService.InsertBulkSubjectAsync`).

As requested, `ICurrentUser` gained `FirstName` / `MiddleName` / `LastName`:

- `Auth/Constants/AuthClaimTypes.cs` — three new claim names.
- `Auth/Services/Login/JWTService.cs` — emits them. `LoginDTO` already carried all three
  name parts, so this is purely additive; **tokens issued before this change return null
  for them until the user signs in again.**
- `Auth/Shared/Implementations/CurrentUser.cs` — reads them, falling back to the standard
  `GivenName` / `Surname` claim types.

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

- `EndorsementSubmissionService.InsertEmailInvitationRequestAsync` (single order, and
  the AI assistant's confirm-draft path, which routes through it)
- `BulkSubmissionProcessorService` (bulk CSV rows)

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

Throughput knobs, all constants at the top of `OMSTicketingRepository`:

| Knob | Value | Why |
|---|---|---|
| `ClaimBatchSize` | 50 | each ticket is 3 stored-procedure round trips to a remote legacy SQL Server, so smaller than the email queue's 100 |
| `PerClientSliceSize` | 30 | one large bulk upload cannot starve other clients |
| `MaxTicketAttempts` | 5 | poison-message guard, same as `MaxEmailSendAttempts` |
| `MaxDegreeOfParallelism` | 3 | concurrent OMS calls |
| tick interval | 10s | same as the bulk and email jobs |

`MarkTicketFailedAsync` takes an `isRetryable` flag. A **non**-retryable failure sets
`TicketAttempts` straight to the cap rather than incrementing, so a condition that cannot
resolve itself is not re-attempted five times over 50 seconds.

`GetTicketPayloadsAsync` left-joins `PersonalDetails`, `PackageDetails` (by name — see
below) and `UserDetails` in one round trip. Left joins are deliberate: a missing row must
still come back so the service can park it *with a reason* instead of silently dropping
it from the batch.

### Step 6 — Payload mapping

`Services/OMSTicketing/OMSTicketPayloadMapper.cs` — pure and static, so the rules are
testable without a database or a live OMS connection.

| OMS field | Source |
|---|---|
| `FirstName` / `MiddleName` / `LastName` | `EmailInvitationRequest` (`MiddleInitial` → `MiddleName`) |
| `DateOfBirth` | `PersonalDetails.DOB`, null at enrolment |
| `EmailAddress` | `EmailInvitationRequest.EmailAddress` |
| `PhoneNumber` | `PersonalDetails.MobileNumber` ?? `EmailInvitationRequest.MobileNumber`, normalised |
| `SSSIDNumber` / `TIN` | `PersonalDetails`, blank at enrolment |
| `Remarks` | literal `"Remarks"` |
| requestor name / email | Auth directory lookup |
| `Site` | `UserDetails.Site` |
| `TurnAroundTimeID` | `2` |
| `ReportTypeID` | parsed from `PackageDetails.PackageDescription` |
| `CountryID` / `ProvinceID` / `CityID` | `0` |
| `Address` / `PostalCode` | `""` |

Two normalisations worth reviewing:

- **Phone** — strips spaces and dashes, converts `+63…` / `63…` to the local `0…` form,
  and adds a missing trunk zero to a bare `9…` number. Returns null if the result is not
  11–12 digits, which parks the order rather than letting OMS reject it.
- **SSS / TIN** — kept only when the digit count is exactly right; otherwise sent blank.
  The field is optional, so a malformed value must not fail the whole ticket.

**`ReportTypeID` is the fragile part of this feature and deserves attention in review.**
`EmailInvitationRequest.SelectPackage` is free text holding a *package name* with no
foreign key, and the numeric report type is stored in `PackageDetails.PackageDescription`,
a 500-character free-text column. The mapper matches the package by name, then takes the
leading digits of the description (so `"182"`, `" 182 "` and `"182 - Criminal Records
Check"` all work). If the package does not match or the description does not yield a
positive integer, the order is parked as `Error` with the reason and **OMS is never
called** — no wasted PO validation, and it is visible for someone to fix the package
configuration.

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

Failure classification:

- `BadRequestException` from OMS (invalid requestor, exhausted PO) → **not** retryable.
  Retrying cannot fix either; it needs a human.
- `InternalServerException` / connectivity → retryable, consumes one attempt.
- Mapping failure → not retryable, and no OMS call at all.

`BackgroundJobs/OMSTicketing/` holds the thin Quartz shell plus its trigger setup,
registered via `services.ConfigureOptions<OMSTicketingBackgroundJobSetup>()`.

### Step 8 — API slices and gateway

Two query slices under `Features/OMSTicketing/Query/`, one folder per operation as the
guide requires:

| Slice | Carter route | Gateway route |
|---|---|---|
| `GetTicketedOrders` | `getticketedorders` | `/ats/getticketedorders` |
| `GetTicketStatusCounts` | `getticketstatuscounts` | `/ats/getticketstatuscounts` |

Both registered in `Path/ATSPaths.cs` — the typed module is the only source of gateway
routes at runtime. Verify with `GET /__routes`.

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
it is the one value a user copies out of this screen), Delivery Date, Status.

Registered as ATS module **14** (`ticketingstatus`, "Ticketing Status") in all three
places that must agree: `AtsModuleIds`, the UI `ModuleList`, and the seed data. Because
`ATSDatabaseExtensions` only seeds modules into an empty table, the existing
`BackfillBulkUploadsModuleAsync` was generalised to
`BackfillModuleGrantedWithNewOrderAsync(moduleId)` and is now called for both modules —
so existing databases get module 14 granted to everyone who already has New Order.

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
  stylesheet went from 360 lines to 92, keeping only what is genuinely its own (the
  email progress bar, order-type chip, subject count).
- Ticketing's scoped stylesheet is 45 lines: the monospaced ticket number, the stacked
  status cell, and the clamped error text.
- Unused hooks (`bulk-uploads-scope` wrapper, two `TableClass` values matching no rule)
  were removed.

The rule behind this was written into `docs/feature-development-guide.md` under
**"Reuse existing styles; do not duplicate a design"**, with a matching entry in the
definition-of-done checklist.

---

## 3. Tests

| Suite | Result |
|---|---|
| `OMSTicketPayloadMapperTests` (new) | 29 passed |
| `OMSTicketingProcessorServiceTests` (new) | 9 passed |
| Full `ATS.UnitTests` | 174 passed |
| `Auth.UnitTests` (JWT claims changed) | 99 passed |
| `dotnet build 1CibiPlatform.sln` | succeeded |

The processor tests pin the behaviour that is easy to regress: the reference number is
the invitation id; a mapping failure parks **without** calling OMS; a `BadRequestException`
is not retryable while an `InternalServerException` is; one failing order does not stop
its siblings; and a claimed order with no payload is parked rather than left stranded.

> `dotnet format` initially rewrote trailing whitespace in ~70 unrelated Auth and ATS
> files. Those were reverted, so the diff is scoped to this feature.

---

## 4. Not done / worth a decision

1. **Not verified against a live OMS.** Everything is tested against a faked
   `IOMSTicketCreator`. The end-to-end check against the OMS UAT database — enrol an
   order, wait a tick, confirm a real `ticket_no` and that the ticket carries the
   `EmailInvitationID` as its reference — still needs to be run.
2. **No integration test.** The unit tests cover the processor's decision-making, but
   the `SKIP LOCKED` claim itself is only proven by the pattern it copies. A Testcontainer
   test asserting that two parallel `ProcessAsync` calls produce exactly one OMS call per
   row would be the honest proof.
3. **Throughput knobs are constants, not configuration.** This matches the bulk and email
   jobs, which also hardcode theirs. Easy to move to `appsettings` if you want them tuned
   without a deploy.
4. **`ReportTypeID` depends on a free-text convention.** Storing the numeric id in
   `PackageDescription` works, but nothing enforces it — renaming a package silently
   breaks ticketing for orders that reference it (they park as `Error`, visibly). A real
   `ReportTypeId` column on `PackageDetails` would remove the guesswork.
5. **Existing tokens lack the new name claims** until users sign in again, so
   `ICurrentUser.FirstName` / `LastName` read null for them. The ticketing job does not
   depend on this — it reads the Auth directory — but any other new consumer would.
6. **Orders created before this change have `TicketStatus = null`** and are therefore not
   queued and not shown on the new screen. If they should be back-filled and ticketed,
   that is a deliberate one-off `UPDATE` and should be a separate decision.
