# ATS Audit Trail — Implementation Review

Records every state-changing ATS operation — who did it, what it carried, whether it
worked — writes it asynchronously so no request pays for it, exposes it as a new
**Audit Trail** screen that mirrors Ticketing Status, and deletes entries after 30 days.

Branch: `feature/Add-Audit-Logs-ATS`. Follows `docs/feature-development-guide.md`.

---

## 1. What was decided, and why

### Why a MediatR behaviour, not middleware

`ValidationBehavior<TRequest, TResponse>` is constrained
`where TRequest : ICommand<TResponse>`. MediatR only applies an open behaviour whose
generic constraints the request satisfies, so `AtsAuditBehavior` carries the same
constraint and **queries never enter it** — a compile-time guarantee rather than a
runtime type check. ATS has 22 `ICommand<>` and 13 `IQuery<>` types, which is exactly the
write/read split the trail wants.

ASP.NET middleware was the alternative and is worse here: it sees an HTTP body, not a
typed command, so it cannot name the action, cannot redact by property name, and cannot
tell a Carter read route from a write route without a second list to maintain.

### The behaviour is container-wide, so it also filters by module

`AddOpenBehavior` registers `IPipelineBehavior<,>` into the **one shared container**, and
every module calls its own `Add*MediaTR` against that same `IServiceCollection`. So an
open behaviour registered by ATS wraps *every* module's commands, not just its own.

The first build of this screen filled up with Auth's `LoginWeb`, `Logout`,
`IsAuthenticated` and `GetNewAccessToken` rows because of exactly that. Those are already
covered by PlatformLogging; this table is the ATS trail.

`IsAtsCommand` fixes it by checking the command's root namespace, resolved once per closed
generic type alongside `IsAudited`. `LoggingBehavior.GetApplicationName` reads the root
namespace the same way, so this is the established way to tell modules apart here.

A non-ATS command is passed straight through — not audited, and not otherwise interfered
with. `AtsAuditBehaviorTests` covers both that and the fact that ATS *failures* still get
recorded, so the filter cannot be quietly over-broad.

### Behaviour order

Registered **after** `ValidationBehavior` and `LoggingBehavior` in `AddATSMediaTR`. A
request rejected as invalid never reached a handler and must not be recorded as an action
someone took. This is asserted indirectly: the audit behaviour only sees requests that
got past validation.

### Skipping is opt-out, not opt-in

`[SkipAudit]` marks a command as unrecorded. Only `AskAtsAssistantCommand` carries it —
a conversational turn whose question text would bury the log in noise.

`ConfirmOrderDraftCommand` is **not** skipped, despite also being an AI assistant command:
it calls `InsertEmailInvitationRequestAsync` and creates a real order, so it is a write
like any other. This is the reason the attribute is opt-out — a new command is audited by
default, and forgetting to annotate one leaves noise in the trail rather than a hole in
it.

Report downloads (`DownloadIndividualReport`, `DownloadMultipleOrderRecords`) are audited.
In a background-screening system, who pulled which report is exactly the access worth a
trail.

### Redaction, not omission

The payload is stored, but `AtsAuditRedactor` masks any property whose name is in a
case-insensitive set — `SSS`, `TIN`, `DOB`, `HashToken`, `Password`, `Signature`, and
so on — recursing through nested objects and arrays, since `AddUserCommand` and
`AddClientCommand` both take collections.

Two limits worth knowing:

- Masking is **by property name**. A new command carrying a sensitive field under a new
  name must add it to `SensitivePropertyNames`; nothing else in the pipeline will notice
  that it leaked.
- The serialized payload is capped at 8 000 characters. A bulk upload command carries
  every row of a spreadsheet, so an oversized payload is replaced by a marker rather than
  writing a megabyte row. The entry itself is still recorded — who did what is the point,
  the body is supporting detail.

An unserializable payload also degrades to a marker. Recording an action must never fail
because of the shape of its payload.

### Asynchronous by bounded channel

`AtsAuditWriter` is a singleton wrapping `Channel.CreateBounded` with
`FullMode = DropWrite`, drained by `AtsAuditDrainService : BackgroundService`. This is
the same shape `PostgreSqlBatchingSink` already uses for platform logs.

`DropWrite` over `Wait` is deliberate: waiting would push database latency back onto the
request thread, which is the one thing the queue exists to prevent. A dropped entry is a
real gap, so it is logged as a warning.

The behaviour reads every `ICurrentUser` value **synchronously** into the entry before
enqueueing. The drain runs on its own scope long after the response was sent, where there
is no `HttpContext` and no claims principal to read.

Not Quartz: the other ATS jobs tick on a schedule to find work already in the database.
The drain is a continuous consumer of an in-memory queue, which is what `BackgroundService`
is for. `StopAsync` closes the channel so a graceful shutdown drains the backlog instead
of losing it.

### Auditing must never break the audited action

Three layers of this:

1. `Record` wraps everything in a try/catch and logs rather than throws.
2. `TryEnqueue` never blocks and never throws.
3. A handler that throws is recorded as `Failure` **and the exception is rethrown**, so
   the caller still gets its normal error response from the global handler.

All three are covered by `AtsAuditBehaviorTests`.

### No foreign key to UserDetails

The trail records who acted at that moment. It has to survive the user being deactivated,
reassigned to another client, or removed. That is also why `AtsRoleId`, `AtsClientId`,
`Site` and `IsPlatformSuperAdmin` are copied onto the row rather than joined at read time
— a later role change or site transfer must not rewrite history.

### Field-level before/after, via a SaveChanges interceptor

The payload records what a command *asked for*. It does not say what the value was before,
which is usually the question being asked — "who set this to inactive, and what was it?"

`AtsAuditChangeInterceptor` answers that by reading EF Core's change tracker in
`SavingChanges`. **Not `SavedChanges`**: once the save completes EF marks entries
`Unchanged` and the original values are gone.

For a genuinely tracked edit that is all it takes: the tracker already holds both values.
No per-command diff code, and a property added later is picked up automatically.

#### Detached updates need the row read back

Most ATS edit repositories do **not** leave the entity tracked. `GetPackageAsync`,
`GetRoleAsync` and `GetModuleAsync` all fetch with `AsNoTracking()`, the service mutates
that detached object, and `EditPackageAsync` calls `DbSet.Update()` on it.

EF then marks **every** property modified with `OriginalValue == CurrentValue` — there is
no before-image at all. The first version of this interceptor read only the tracker, so
those edits silently recorded `Changes = null`; `EditPackage` rows in the trail had no
diff. The unit test passed because it used a tracked entity, which is not what the
repositories do.

So when an entry has no usable originals, `LoadOriginalsAsync` calls
`GetDatabaseValuesAsync` and copies the stored row over the entry's original values before
the `UPDATE` overwrites it. That costs one extra `SELECT` per detached edit, on the write
path only.

Two consequences worth knowing:

- The diff is computed **by value**, not from `IsModified` — a detached `Update()` flags
  every property, so trusting `IsModified` would report the whole row as changed.
- A re-submitted form that changed nothing records no entry, rather than a row claiming
  every column changed.

The captured diffs go into `IAtsAuditChangeCollector`, a **scoped** service: the
interceptor writes to it during `SaveChanges`, and `AtsAuditBehavior` reads it *after*
`next()` returns. It cannot be done in the drain, where the DbContext is long disposed.

Because the collector and interceptor are scoped, `AddDbContext` uses the
`(serviceProvider, options)` overload — the first interceptor in this codebase.

#### What it does not see

`ExecuteUpdateAsync` issues SQL directly and never populates the change tracker. There are
19 such calls in ATS — ticket status, email status, bulk upload claims — so **those
commands record no field changes**. `RetryTicket` is one of them.

That is deliberate rather than an oversight: those paths use `ExecuteUpdate` for
throughput and for `FOR UPDATE SKIP LOCKED` semantics, and converting them would change
the write path of the ticketing and email jobs. The order lifecycle is already covered by
`OrderStatusHistory`, which records previous/new status properly.

`Changes` is **null**, not `[]`, for those — so the dialog can distinguish "not captured
for this action type" from "nothing changed" and says which.

#### Deliberately not redacted

Unlike `Payload`, the diff stores real values. A masked diff (`*** → ***`) tells you a
field changed but not what it was, which defeats the point.

The consequence, stated plainly: **this column holds historical government IDs and
birthdates for 30 days**, making it as sensitive as the source data. That is the same
access class the screen already enforces — platform super admin only — but it is the
reason that restriction now matters more than it did.

Both are capped at `AtsAuditRedactor.MaxPayloadCharacters`; an oversized diff stores a
marker, and the collector stops at 50 entities so a bulk save cannot write an unbounded
row.

### Site is resolved by the drain, not the request

Role and client are JWT claims, so `ICurrentUser` hands them over for free. **Site is not**
— it lives on `UserDetails`, which means a database lookup.

Doing that lookup in the behaviour would put a round trip on every audited request, which
is the exact cost moving the write off the request thread was meant to avoid. So
`AtsAuditDrainService.ResolveSitesAsync` fills it in per batch instead: one query for up to
`BatchSize` entries, in a scope the drain already opens.

`UserDetails` is keyed `(UserId, ModuleId)` — one row per module grant, each carrying the
same Site — so the query groups by `UserId` and takes any one of them, the same assumption
`OMSTicketingRepository`'s `Take(1)` makes. A user with no ATS grant at all (a platform
super admin who was never given a module) simply has no site; the entry still stands.

The trade: an entry's Site reflects the user's site *when the drain ran*, typically under a
second later, not the exact instant of the action. For a value that changes on transfer
rather than per request, that is not a meaningful difference.

### Super admin only

`AtsAuditService.CanRead` checks `ICurrentUser.IsPlatformSuperAdmin`. It deliberately does
**not** use `IAtsAccessScopeResolver`: this screen is not client-scoped, because a trail
the audited user can read is a weaker control.

A caller without the right reads an **empty page**, not a 403 — the same way every other
ATS list treats an out-of-scope caller. The UI enforces the same rule independently by
listing module 15 in `RestrictedAdministrationModuleIds`.

---

## 2. Schema

`ats."AuditTrail"`, one row per audited command.

| Column | Type | Notes |
|---|---|---|
| `AuditEntryId` | `uuid` | `Guid.CreateVersion7()`, `ValueGeneratedNever()` — minted by the behaviour so an entry keeps its recorded order even though the drain writes it later |
| `OccurredAt` | `timestamptz` | |
| `Action` | `varchar(120)` | Command name, suffix trimmed: `AddUserCommand` → `AddUser` |
| `Area` | `varchar(80)` | Feature folder from the namespace: `UserManagement` |
| `Outcome` | `varchar(20)` | `Success` / `Failure` |
| `FailureReason` | `varchar(500)` | Truncated exception message; null on success |
| `DurationMs` | `int` | |
| `UserId` | `uuid?` | |
| `UserEmail` / `UserFullName` | `varchar(255)` | |
| `AtsRoleId` / `AtsClientId` | `int?` | As they were at the time |
| `Site` | `varchar(100)` | As it was at the time. Resolved by the drain, not the request — see below |
| `IsPlatformSuperAdmin` | `bool` | As it was at the time |
| `IpAddress` | `varchar(64)` | |
| `TraceId` | `varchar(64)` | Ties the entry back to `logging.log_events` |
| `Payload` | `jsonb` | Redacted command JSON |
| `Changes` | `jsonb` | Field-level before/after values. **Not** redacted — see below. Null when nothing tracked changed |

Indexes: `(OccurredAt DESC, AuditEntryId DESC)` for the keyset page, `(OccurredAt)` for
the retention sweep.

`jsonb` rather than `text` so the payload can be queried directly later. One consequence
to know: Postgres stores a *parsed* representation, so the text that comes back is
re-emitted with its own spacing — it round-trips as JSON, not byte for byte.

---

## 3. Retention

`AtsAuditRetentionService` is modelled directly on `PlatformLogRetentionService`: a
`PeriodicTimer` on a configurable interval, deleting in batches via `ExecuteDeleteAsync`
until a pass comes back short, so one sweep can clear a large backlog without holding a
single enormous `DELETE` open.

`AtsAuditOptions` (section `AtsAudit`) mirrors `PlatformLoggingOptions`:

| Setting | Default |
|---|---|
| `Enabled` | `true` |
| `BufferSize` | `10 000` |
| `BatchSize` | `100` |
| `RetentionEnabled` | `true` |
| `RetentionDays` | `30` |
| `RetentionIntervalHours` | `24` |
| `RetentionBatchSize` | `5 000` |

Every value has a working default, so an absent section is valid and the feature ships
without an appsettings change.

---

## 4. Screen

`/s&i/ats/audittrail`, ATS module **15**, restricted to platform super admins.

Seven columns, the same count as Ticketing Status: **When** (relative, absolute on
hover), **User** (name over email), **Action**, **Area**, **Outcome** (pill, with the
failure reason inline), **Duration**, **Details**. The rest of the row — payload, IP,
trace id, user id, role, client and site — lives in the detail dialog.

### Styling

Everything structural is a shared class already in `wwwroot/css/ats.css`:
`.ats-management-page`, `.ats-status-board-intro`, `.ats-segmented` /
`.ats-segment-btn`, `.ats-status-board-card`, `.ats-cell-lead*`, `.ats-cell-tag`,
`.ats-cell-muted`, `.ats-cell-action`, `.ats-console-empty-state`.

Success reuses the existing `.ats-status-pill.done` and Failure `.ats-status-pill.error`,
so no new hue was introduced for a third status vocabulary.

One rule was **generalized rather than copied**: the navy gradient dialog header that
`PreviewComponent` introduced and `BulkUploadSubjectsDialog` had already duplicated. It
now lives in `ats.css` as `.ats-dialog-headline*`, which the audit detail dialog uses.
The two existing copies still carry their own selectors and can be folded onto the shared
class in a later behaviour-preserving pass — worth doing, since three copies was the
point at which this stopped being acceptable.

Scoped CSS is limited to what is genuinely new: the failure-reason clamp on the board, and
the payload viewer in the dialog.

---

## 5. Tests

**Unit** — `AtsAuditRedactorTests` (11): masks each sensitive name, recurses into nested
objects and arrays, matches case-insensitively, verifies the secret is absent from the
whole document rather than just the property checked, caps oversized payloads, degrades
instead of throwing on an unserializable payload.

`AtsAuditBehaviorTests` (14): success and failure outcomes, the caller captured as they
were, payload redacted through the behaviour, **the exception still rethrown**, a throwing
writer not failing the request, `[SkipAudit]` and `Enabled = false` recording nothing,
over-long failure reason truncated to the column width, a **command from another module
ignored**, an ATS failure still recorded, `Area` resolved from the feature folder, field
changes recorded from the collector, `Changes` left **null** when nothing tracked changed,
and the diff **not** redacted while the payload still is.

Its command fakes live in `AtsAuditBehaviorTestCommands.cs` rather than the test file,
because the module filter and `Area` resolution both read the namespace — a fake has to
sit in `ATS.Features.*` (or, for the ignored case, `Auth.Features.*`) to exercise either.

`AtsAuditServiceTests` (9): a non-super-admin gets an empty page **and the repository is
never asked**, cursor round-trips, the extra row is trimmed, a malformed cursor self-heals
to the first page, the outcome filter is canonicalized and an unknown one ignored.

**Integration** (24, Postgres Testcontainer): newest-first ordering, the keyset walk
visiting every entry exactly once, no repeats when timestamps are identical (the id
tiebreaker), every filter, search covering who/what/site but **not** the payload, all
fields round-tripping, counts ignoring the outcome filter but respecting the others, the
site lookup returning one row per user across several module grants and nothing for a user
with no ATS access, and the retention sweep deleting only past-cutoff rows.

The interceptor has four of its own, all against real Postgres: before/after on a tracked
edit, **before/after on a detached `AsNoTracking()` + `Update()` edit** (the regression
above, and the pattern the real repositories actually use), nothing captured when a
detached save changed no values, and nothing captured for a read-only save.

Note: `BaseIntegrationTest` truncates `ats."AuditTrail"` between tests — without that,
entries leak across runs.

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS"   # 434 passed
dotnet build 1CibiPlatform.sln
```

Three Auth email integration tests fail on this branch. They fail identically on a clean
checkout with these changes stashed, so they are pre-existing and unrelated.

---

## 6. Known gaps

- **Masking is name-based.** A sensitive field under an unrecognised property name will
  be stored in full. `SensitivePropertyNames` is the single place to fix that.
- **A dropped entry is a real gap.** Under sustained load past `BufferSize`, entries are
  dropped rather than requests slowed. That is the intended trade, but the warning it logs
  is worth alerting on.
- **A failed batch is not retried.** Retrying forever would block every entry behind it.
  The failure is logged and the loop continues.
- **No field-level diff on the 19 `ExecuteUpdateAsync` paths.** Covered above; the UI says
  so rather than implying nothing changed.
- **A detached edit costs one extra `SELECT`.** Only on the write path, only when the
  tracker has no before-image. If a repository is ever converted to a tracked
  load-then-save, that query disappears on its own.
- **Rows written before this shipped have `Changes = null`.** There is nothing to backfill
  from — the before values were never captured.
- **`Changes` stores unredacted values.** A deliberate choice, but it means the audit table
  carries a 30-day history of old government IDs and birthdates. If retention or access
  ever loosens, revisit this first.
- **The module filter is a namespace string.** `IsAtsCommand` matches on the `ATS.`
  namespace prefix, so a command placed outside that root would be silently skipped. This
  is the same trade `LoggingBehavior.GetApplicationName` already makes.
- **The behaviour is scoped to ATS only.** It lives in the ATS module because
  BuildingBlocks references neither Auth (`ICurrentUser`) nor EF Core. Extending it to
  another module means moving the shared pieces down and widening `IsAtsCommand`, not just
  adding a registration.
