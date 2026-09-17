# Package follow-up email — code explanation

Companion to `ats-package-follow-up-email.md`, which covers behaviour. This one covers where the
code lives and why it is shaped the way it is.

## The pieces

| File | Role |
| --- | --- |
| `Data/Entities/EmailInvitationRequest.cs` | `LastFollowUpSentDate` — the once-per-day guard **and** the reminder-copy signal |
| `Data/EntityConfiguration/EmailInvitationRequestConfiguration.cs` | nullable, mapped to `date`, deliberately un-indexed |
| `Migrations/ATS/…AddFollowUpQueuedAtToEmailInvitationRequest.cs` | historical: the fire-once column + the backlog backfill |
| `Migrations/ATS/…AddLastFollowUpSentDateToEmailInvitationRequest.cs` | the daily-reminder column; no backfill, by design |
| `Migrations/ATS/…DropFollowUpQueuedAtFromEmailInvitationRequest.cs` | removes the superseded column |
| `Data/Repository/EmailInvitations/ATSRepository.EmailInvitations.cs` | `ReleaseDueFollowUpInvitationsAsync` — the whole feature, in one statement |
| `Data/Cache/EmailInvitations/…Cache.cs` | evicts the two rollups a status change affects |
| `Services/EndorsementSubmission/EndorsementSubmissionService.cs` | `ReleaseDueFollowUpEmailsAsync` — release, record history, log |
| `BackgroundJobs/FollowUpEmail/` | hourly Quartz job + its setup |
| `Services/EmailService/ATSEmailService.cs` | `BuildApplicationFormReminderNotification` |
| `Services/EmailNotificationProcessor/EmailNotificationProcessorService.cs` | picks reminder vs. invitation copy; writes `InvitationEmailSent` history in the same transaction as the sent status |
| `Constants/FollowUpSchedule.cs` | the one `Asia/Manila` definition, shared by the release query and the remaining-count |
| `Services/Report/ReportService.cs` | `CalculateFollowUpEmailsRemaining` — the board's "Follow-ups Left" number |
| `Component/ATS/Orders/SearchReportComponent.razor` | the column that renders it |

## The release statement

One raw-SQL CTE, modelled on the existing claim query in the same file:

```sql
WITH due AS (
    SELECT eir."EmailInvitationID"
    FROM ats."EmailInvitationRequest" eir
    JOIN ats."PackageDetails" pd ON pd."PackageId" = eir."PackageId"
    WHERE …
      AND eir."LastFollowUpSentDate"
          IS DISTINCT FROM (now() AT TIME ZONE {4})::date
      AND (now() AT TIME ZONE {4})
          >= (eir."OrderCreatedAt" AT TIME ZONE {4}) + interval '1 day'
      AND (now() AT TIME ZONE {4})
          <  (eir."OrderCreatedAt" AT TIME ZONE {4})
             + make_interval(days => pd."FollowUpEmail" + 1)
    ORDER BY eir."OrderCreatedAt"
    LIMIT 200
    FOR UPDATE OF eir SKIP LOCKED
)
UPDATE ats."EmailInvitationRequest" t
SET "EmailSentStatus" = Pending, "EmailSendAttempts" = 0,
    "EmailClaimedAt" = NULL, "EmailSentAt" = NULL,
    "LastFollowUpSentDate" = (now() AT TIME ZONE {4})::date
WHERE t."EmailInvitationID" IN (SELECT … FROM due)
RETURNING t.*;
```

`{4}` is `FollowUpTimeZone`, the `Asia/Manila` constant at the top of the same file. It is a
parameter rather than an inlined literal for the usual reason — it is the only form `FromSqlRaw`
will accept without string concatenation.

The three added clauses are the whole of the daily behaviour: **dedupe** (one per local day),
**start** (the day after the order, at its time of day), and **stop** (the N-day window). Reading
them in that order is the fastest way to reason about whether a given row is due.

Six things about this shape are load-bearing:

**`FOR UPDATE … SKIP LOCKED`.** Quartz here is clustered and persistent, so two replicas can in
principle overlap despite `[DisallowConcurrentExecution]` (see §7 of `ats-email-delivery.md`).
Skip-locked means the second one steps over rows the first is already releasing instead of
blocking on them or double-releasing.

**The stamps are in the same `UPDATE` as the requeue.** If `LastFollowUpSentDate` were written
separately, a crash between the two writes would leave a requeued row with no date — and the next
hourly pass would chase that candidate again the same day. One statement, so the guarantee is the
transaction's, not the scheduler's.

**`IS DISTINCT FROM`, not `<>`.** `NULL <> today` evaluates to NULL, not true, so a plain `<>`
would exclude every row that has never been chased — i.e. the entire feature would silently never
fire. This is the single easiest clause in the query to "simplify" into a no-op.

**The window is half-open: `>= order + 1 day` and `< order + (N+1) days`.** That is N whole days
wide starting at the first eligible moment, which is what makes `FollowUpEmail = 2` send twice.
Dropping the `+ 1` sends once; making the upper bound `<=` sends N+1 times. Both look plausible in
review and neither fails loudly.

**`LIMIT 200`.** Same bound as the claim query. A pass is a slice, not the whole backlog; the job
runs hourly and the work is measured in days, so there is never pressure to drain it in one go.

**It does not touch `HashToken` or `HashTokenCreatedAt`.** This is the reason the method exists at
all instead of reusing `RequeueEmailInvitationAsync`, which rotates both — see §8 of
`ats-email-delivery.md`.

`EmailSendAttempts = 0` is correct rather than sloppy: a reminder is a fresh delivery and the cap
of five applies to it in its own right, exactly as it does for a resend.

## Why the join is allowed

`PackageId` is a configured FK to `PackageDetails`, so reading `pd."FollowUpEmail"` in the
predicate is a supported relationship and not an ambient assumption about table shapes.

## Why no index on either follow-up column

`EmailInvitationRequest` is write-hot — the configuration file carries its own note about not
adding redundant indexes to it. The chaser runs once an hour and its predicate already narrows
hard on `EmailSentStatus`, which is indexed. An extra index would cost every insert and update in
the email pipeline to save an hourly query that is already cheap.

Note that the `AT TIME ZONE` expressions are not sargable against a plain index on
`OrderCreatedAt` anyway — they are computed per row. That is acceptable at this cardinality and
frequency; if it ever stops being, the fix is an expression index, not a plain one.

## The service method

`ReleaseDueFollowUpEmailsAsync` calls the repository, records one
`ApplicationFormFollowUpSent` history entry per released row via `RecordManyAsync`, logs the count,
and returns it. It does not send. It contains no `try`/`catch` — feature code does not catch in
this codebase.

## The job

`FollowUpEmailBackgroundJob` is a near-copy of `EmailNotificationBackgroundJob`:
`[DisallowConcurrentExecution]`, a scope per execution, the `["Application"] = "ATS"` logging
scope, and a `try`/`catch` **inside** the job. Jobs catch; feature code does not — an exception
escaping into Quartz is a misfire, not a handled error, and `CustomExceptionHandler` is not in the
call path.

`OperationCanceledException` is swallowed when the token is the one that requested it, because that
is shutdown, not failure.

Registered via `services.ConfigureOptions<FollowUpEmailBackgroundJobSetup>()` alongside the other
four jobs. Its `JobKey` and trigger identity are distinct — Quartz's store here is shared, so a
duplicate identity would collide with another node's job rather than fail locally.

Hourly, because the unit is days.

## Choosing the copy

`BuildApplicationFormReminderNotification` lives on `IAtsEmailSender`, not on the shared
`IEmailService`. The first-invitation body, `SendAppplicationFormNotification`, is a BuildingBlocks
contract that Auth and the test fakes also implement, and none of them have a package follow-up to
compose. Only ATS chases, so only ATS declares it.

It composes; it does not send. The caller pairs the returned body with the reminder subject and
hands both to `SendATSEmailWithResultAsync`, so a reminder travels the same pooled, capped, paced
path as every other ATS message.

The processor decides which to use:

```csharp
var isFollowUp = request.LastFollowUpSentDate is not null && request.EmailSentAt is null;
```

The date alone is not sufficient — it stays set once stamped, so a row whose reminder was already
delivered would keep claiming to be one. `EmailSentAt` is cleared by the same release `UPDATE`, so
the pair reads as "queued as a follow-up, not yet sent".

**This used to read a separate `FollowUpQueuedAt` column, and that was a bug.** That column's
migration backfilled every pre-existing row with `now()` — correct for its original job (keeping a
brand-new chaser off the historical backlog), fatal for this one. An operator resending a legacy
order clears `EmailSentAt`, and with the backfilled stamp already present the row satisfied both
halves of the check, so a candidate who had never been chased received "we have not yet received
your form". Reading the column that actually gates the release removes the second source of truth
and the defect with it: `LastFollowUpSentDate` is never backfilled, so null means never chased.

The three `#region Reminder vs. first-invitation copy` tests in
`EmailNotificationProcessorServiceTests` pin all three states — chased-and-pending, never-chased,
and already-delivered. Worth having because the failure is silent: the send succeeds either way and
the only symptom is wrong wording in someone's inbox.

The link is rebuilt from `request.HashToken`, which the chaser left alone — which is why reusing
the candidate's original link needed no change in the sender at all.

## The bulk-completion fix that came with this

Enabling chasers on bulk-sourced rows exposed a latent bug. `GetCompletedBulkEmailFilesAsync`
returned any file whose in-flight count had reached zero, and `RaiseAsync` had no de-duplication —
so a chaser putting a delivered row back to `Pending` and then to `Done` made the file "complete"
a second time, and the uploader was notified again days after they stopped caring. An operator
resend on a bulk row did the same thing.

Rather than scoping the chaser away from bulk rows — which would have silently excluded most
orders — the notification was made idempotent: the repository now excludes files that already have
a `BulkEmailsCompleted` notification with that `EntityId`. The check is in SQL, one round trip
instead of one per file, and in the repository because the service has no reason to know that
`EntityId` carries the file id.

## Validation

`FollowUpEmail` is bounded 0–90 in both the add and edit package validators
(`AddPackageHandler.cs`, `EditPackageHandler.cs`), with a matching `Max="90"` on the numeric field
in **both** `AddPackageComponent.razor` and `EditPackageComponent.razor`. The edit form was missing
its `Max` until the daily-reminder change; the server rejected the value either way, but the field
let the operator type it first.

Under daily reminders the bound is also a volume bound, not just a sanity bound: N is now how many
emails a single unanswered order can generate.

## Recording the delivery

`EmailNotificationProcessorService.ProcessForPendingStatusAsync` used to flip the sent status on
its own. It now writes the status and the history together:

```csharp
await TransactionRunner.RunAsync(
    _unitOfWork,
    async () =>
    {
        await _repository.UpdateBulkEmailInvitationRequestForSentEmailAsync(successList);

        await _orderHistoryService.RecordManyAsync(
            successList.Select(request => request.EmailInvitationID).ToList(),
            OrderHistoryEventType.InvitationEmailSent,
            null,
            OrderStatus.PendingCandidateInfo,
            cancellationToken,
            OrderHistorySource.System);
    },
    cancellationToken);
```

Four decisions in that block:

**`RunAsync`, not `SideEffectGuard`.** The two are easy to confuse here (see
`docs/features/transaction-runner/transaction-runner.md` §4). A notification *after* a submission is
best-effort; this is the record OF the send, so a failure has to take the status with it and let the
next pass retry both.

**`RecordManyAsync`, not a loop.** A pass carries up to 200 invitations and `AddAsync` saves per
row. `AddRangeAsync` underneath is one insert for the slice.

**`null` previous status, unchanged new status.** Delivering an invitation does not advance the
order's lifecycle — the candidate still has to fill the form in — so a previous/new pair would
invent a transition that never happened. Same choice `ReleaseDueFollowUpEmailsAsync` makes.

**`OrderHistorySource.System`.** No human asked for this; the source column is how the history
distinguishes that from an operator resend.

The inline path in `EndorsementSubmissionService.InsertEmailInvitationRequestAsync` needed no
`TransactionRunner` call of its own — it was already inside one, so the new `RecordAsync` simply
joins the existing transaction next to the status update it describes. A data order records nothing,
because no email was sent.

This is also why `_unitOfWork` and `_orderHistoryService` are now constructor parameters on the
processor; `ATSServiceFixture` supplies a loose `Mock<IUnitOfWork>`, which returns a completed task
for each `ITransactionScope` member so the work delegate actually runs.

## The remaining-reminder count

`ReportService.CalculateFollowUpEmailsRemaining` turns three raw columns carried on `ReportRowDTO`
— `PackageFollowUpEmail`, `ChasesCandidate`, `ApplicationFormStatus` — into the board's number.

The columns are carried raw rather than pre-computed **because these rows are cached**
(`ATSCacheRepository.Reports.Cache.cs`). "How many are left" depends on today's date, so a number
baked into the projection would be stale by exactly as long as the cache entry lives. The inputs are
stable; the answer is not.

`PackageFollowUpEmail` is read with a correlated subquery rather than a join, matching how
`HitStatus` is read a few lines above it — the query's shape is one row per invitation, and a join
to `PackageDetails` risks changing that.

The arithmetic compares **dates, not instants**:

```csharp
var daysElapsed = today.DayNumber - orderDate.DayNumber;
var remaining = row.PackageFollowUpEmail - daysElapsed;

return Math.Clamp(remaining, 0, row.PackageFollowUpEmail);
```

Subtracting the instants would disagree with the release query on the morning of each reminder day:
the instant difference is under 24 hours until the order's own clock time arrives, but the reminder
is still due that day. The clamp handles both ends — a clock skew that puts the order in the future
must not report more reminders than the package allows, and an order past its window reports 0.

Null versus 0 is load-bearing and is the reason the return type is `int?`: null means the question
does not apply, 0 means the schedule is spent, and `SearchReportComponent.razor` renders them as a
dash and "Done" respectively.

## The tests

`Test/…/ATS.IntegrationTests/FollowUpEmailIntegrationTests.cs` exercises the release query directly
rather than the Quartz job, because the job is a four-line wrapper and everything worth asserting
lives in the SQL.

The helpers matter as much as the cases. `ManilaDaysAgo(n)` returns a UTC instant that lands *n*
days ago **in Manila**, anchored at 00:30 local:

```csharp
private static DateTime ManilaDaysAgo(int days)
{
    var localDate = ManilaNow.Date.AddDays(-days).AddMinutes(30);

    return TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified),
        ManilaZone);
}
```

Two traps it avoids. Seeding `DateTime.UtcNow.AddDays(-n)` and asserting against a Manila date is
how a suite goes green locally and red for eight hours a day on a UTC agent. And seeding *the
current time of day* would sit exactly on the `>=` boundary, so whether the row released would
depend on which side of the same second the query evaluated.

## Change X, also check Y

| If you change… | Also check… |
| --- | --- |
| The release predicate | `FollowUpEmailIntegrationTests` — the dedupe, start-day, final-day and window-closed cases each pin one clause — **and** `ReportService.CalculateFollowUpEmailsRemaining`, which mirrors the window |
| `FollowUpSchedule.Id` / `.TimeZone` | Both the release query's `AT TIME ZONE` and the remaining-count read it; the two test suites' own `ManilaZone` helpers must match or they assert against a different day |
| `OrderHistoryEventType.InvitationEmailSent` | `OrderStatusHistoryDialog.razor` needs a title, description, tone and icon for it, or the timeline renders the raw event name |
| The processor's constructor | `ATSServiceFixture` builds it by hand; a new dependency there is a compile break in every processor test |
| `ReportRowDTO`'s follow-up fields | `BuildReportRowsQuery` populates them and `CalculateFollowUpEmailsRemaining` consumes them; the cache decorator passes them through untouched |
| `ReportListDTO.FollowUpEmailsRemaining` | The UI DTO must match, and `SearchReportComponent`'s three render branches assume null / 0 / positive are all distinct |
| The reports table's columns | `ReportColumnCount` in `SearchReportComponent.razor.cs` — it sizes the loading skeleton and is not derived from the markup |
| `LastFollowUpSentDate` (name, type, or that it is written in the release `UPDATE`) | It has **two** consumers, not one: the release predicate's dedupe clause and `EmailNotificationProcessorService.isFollowUp`, which selects reminder copy. Plus the entity, the configuration's `date` mapping, the migration, the model snapshot, and the hourly trigger's assumption that a guard exists |
| The Quartz interval in `FollowUpEmailBackgroundJobSetup` | The dedupe column is what makes a sub-daily interval safe; a faster trigger is fine, removing the guard is not |
| `FollowUpEmail`'s 0–90 bound | Both validators **and** both `Max="90"` attributes; `PackageFollowUpValidationTests` pins the server half |
| The package form's label or hint | `AddPackageComponent.razor` and `EditPackageComponent.razor` both carry it, plus the `padding-right` in each `.razor.css` that reserves room for the suffix |
| Anything about who gets chased | `docs/ats-package-follow-up-email.md` — the rules table and the "What not to do" table are the behavioural contract |
