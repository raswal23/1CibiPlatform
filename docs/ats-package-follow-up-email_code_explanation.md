# Package follow-up email — code explanation

Companion to `ats-package-follow-up-email.md`, which covers behaviour. This one covers where the
code lives and why it is shaped the way it is.

## The pieces

| File | Role |
| --- | --- |
| `Data/Entities/EmailInvitationRequest.cs` | `FollowUpQueuedAt` — the fire-once stamp |
| `Data/EntityConfiguration/EmailInvitationRequestConfiguration.cs` | nullable, deliberately un-indexed |
| `Migrations/ATS/…AddFollowUpQueuedAtToEmailInvitationRequest.cs` | column + the backlog backfill |
| `Data/Repository/EmailInvitations/ATSRepository.EmailInvitations.cs` | `ReleaseDueFollowUpInvitationsAsync` — the whole feature, in one statement |
| `Data/Cache/EmailInvitations/…Cache.cs` | evicts the two rollups a status change affects |
| `Services/EndorsementSubmission/EndorsementSubmissionService.cs` | `ReleaseDueFollowUpEmailsAsync` — release, record history, log |
| `BackgroundJobs/FollowUpEmail/` | hourly Quartz job + its setup |
| `Services/EmailService/ATSEmailService.cs` | `BuildApplicationFormReminderNotification` |
| `Services/EmailNotificationProcessor/EmailNotificationProcessorService.cs` | picks reminder vs. invitation copy |

## The release statement

One raw-SQL CTE, modelled on the existing claim query in the same file:

```sql
WITH due AS (
    SELECT eir."EmailInvitationID"
    FROM ats."EmailInvitationRequest" eir
    JOIN ats."PackageDetails" pd ON pd."PackageId" = eir."PackageId"
    WHERE …
    ORDER BY eir."OrderCreatedAt"
    LIMIT 200
    FOR UPDATE OF eir SKIP LOCKED
)
UPDATE ats."EmailInvitationRequest" t
SET "EmailSentStatus" = Pending, "EmailSendAttempts" = 0,
    "EmailClaimedAt" = NULL, "EmailSentAt" = NULL, "FollowUpQueuedAt" = now()
WHERE t."EmailInvitationID" IN (SELECT … FROM due)
RETURNING t.*;
```

Four things about this shape are load-bearing:

**`FOR UPDATE … SKIP LOCKED`.** Quartz here is clustered and persistent, so two replicas can in
principle overlap despite `[DisallowConcurrentExecution]` (see §7 of `ats-email-delivery.md`).
Skip-locked means the second one steps over rows the first is already releasing instead of
blocking on them or double-releasing.

**The stamp is in the same `UPDATE` as the requeue.** If `FollowUpQueuedAt` were written
separately, a crash between the two writes would leave a requeued row with no stamp — and the next
pass would chase that candidate again. One statement, so the guarantee is the transaction's, not
the scheduler's.

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

## Why no index on `FollowUpQueuedAt`

`EmailInvitationRequest` is write-hot — the configuration file carries its own note about not
adding redundant indexes to it. The chaser runs once an hour and its predicate already narrows
hard on `EmailSentStatus`, which is indexed. An extra index would cost every insert and update in
the email pipeline to save an hourly query that is already cheap.

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
var isFollowUp = request.FollowUpQueuedAt is not null && request.EmailSentAt is null;
```

`FollowUpQueuedAt` alone is not sufficient — it stays set forever once stamped, so a row whose
reminder was already delivered would keep claiming to be one. `EmailSentAt` is cleared by the same
release `UPDATE`, so the pair reads as "queued as a follow-up, not yet sent".

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

`FollowUpEmail` is bounded 0–90 in both the add and edit package validators, with a matching
`Max="90"` on the numeric field. The chaser fires on `OrderCreatedAt + interval`, so an unbounded
mistyped value is indistinguishable from "never" until it silently fires years later.
