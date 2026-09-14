# ATS Sender Email Accounts

How ATS registers the mailboxes it sends invitations from, proves their credentials before
trusting them, and automatically moves to the next one when a mailbox is capped, throttled or
failing.

Related: `docs/ats-email-delivery.md` (how a single message is paced, pooled and classified —
read that first if you have not), `docs/ats-notifications.md`,
`docs/feature-development-guide.md`.

---

## 1. What it does and why it exists

ATS used to send every candidate invitation through **one** Gmail account whose app password
lived in `.env` under `Email:ATSGmail`. `smtp.gmail.com` with an app password is a consumer
endpoint: roughly **500 recipients a day** on a free account, roughly **2,000** on Workspace.
Note *recipients*, not messages — Google counts the people you mailed, which is why the
consumption figure here counts recipients too.

The pooling and rate limiting described in `ats-email-delivery.md` keep the sender safely
*under* that ceiling. They cannot raise it. So as volume grew, the outcome was always the same:
the queue hit the daily cap and simply stopped, for up to 24 hours, silently.

This feature makes sender accounts **rows in a table, managed from the ATS console**. When one
account is capped, throttled or rejected, the send moves to the next account by priority and
the queue keeps draining. Throughput now scales with the number of registered mailboxes, and an
operator can see per-account consumption and health without reading a log.

Two things are non-negotiable in the design, and both come from the same worry:

- **An account is only trusted after a code sent through its own credentials, to its own
  mailbox, has been confirmed.** A typo'd app password must fail loudly on the registration
  form, not quietly at 3am as a queue that stopped.
- **A failure about the recipient must never count against the account.** Getting this
  backwards lets one bulk upload of typo'd addresses retire every registered sender in minutes,
  leaving the queue with nowhere to go and nothing actually wrong.

---

## 2. The moving parts

| File | What it is |
|---|---|
| `Data/Entities/AtsEmailAccount.cs` | The account row: identity, credentials, health. Table `ats."EmailAccounts"` |
| `Data/Entities/AtsEmailSendLog.cs` | One row per successful send. Table `ats."EmailSendLog"` |
| `Data/Entities/AtsEmailAccountOtp.cs` | A pending one-time code. Table `ats."EmailAccountOtp"` |
| `Data/Repository/EmailAccounts/AtsEmailAccountRepository.cs` | All persistence. **Uncached on purpose** |
| `Services/EmailAccounts/SmtpAccountPoolRegistry.cs` | Singleton. Owns one pool + limiter per account, and the breaker |
| `Services/EmailAccounts/AtsEmailAccountSnapshot.cs` | An account as the selector sees it — **no password field exists on it** |
| `Services/EmailAccounts/SmtpAccountContext.cs` | One account's pool, limiter and From identity, held together |
| `Services/EmailAccounts/AtsEmailAccountSecrets.cs` | Builds the encryption context that binds a password to its mailbox |
| `Services/EmailService/ATSEmailService.cs` | **The switcher loop** lives in `SendATSEmailWithResultAsync` |
| `Services/EmailService/EmailDeliveryResult.cs` | `EmailFailureScope` and `CanRetryOnAnotherAccount` — the two rules everything turns on |
| `BackgroundJobs/Notifications/AtsEmailSendLogRetentionService.cs` | Hourly sweep of send-log rows past retention |
| `BuildingBlocks/.../AesGcmSecretProtector.cs` | AES-256-GCM over a key from configuration |

The switcher is **two** classes, and it is worth knowing which does what:

- `ATSEmailService.SendATSEmailWithResultAsync` is the **loop** — "try this account, and if it
  refuses in a way another account could fix, try the next one".
- `SmtpAccountPoolRegistry` is the **owner** — it answers "which account is next", holds each
  account's pool and limiter, and records the health that decides the next answer.

---

## 3. Registering an account

The whole point of the flow is that **the row is written first as `Pending`, and a `Pending`
row is invisible to the selector.** A half-finished registration can never reach the queue.

```text
RegisterEmailAccount
  1. validate: unique mailbox, unique priority
  2. protect the app password (AES-256-GCM, context = the mailbox address)
  3. INSERT the row with VerificationStatus = Pending      <- not sendable yet
  4. SendWithCredentialsAsync: a throwaway SMTP session using the SUBMITTED
     credentials, sending the OTP to that same mailbox
       - connect/auth fails -> BadRequestException carrying the classified SMTP
         reason ("authentication failed - check the app password")
  5. store the OTP as a SHA-512 hash with an expiry

VerifyEmailAccountOtp
  6. match on account + purpose + unused + unexpired; compare the hash
  7. success -> VerificationStatus = Verified, VerifiedAt = now
       -> the selector can now see it
```

**Sending the code through the account's own credentials to its own mailbox proves two things
at once.** That the session connected and authenticated proves the app password is right. That
the code arrived in that mailbox proves whoever registered it can read it. A code sent through
*any other* account would mark this one verified on the strength of a different mailbox's
password — precisely the failure the OTP exists to catch. That is why `IAtsEmailSender` has a
separate `SendWithCredentialsAsync` with no failover: failing over during verification would
silently defeat it.

The throwaway session builds its own `SmtpRateLimiter` and `SmtpConnectionPool` and disposes
both after one message. Nothing is cached or reused, because the account has not yet earned a
place in rotation, and a verification send must not queue behind a live account's traffic.

### Codes

- Six digits from `IOtpService.GenerateOtp()`, stored as a **SHA-512 hash** via `IHashService`.
  An OTP readable in the database is not a second factor.
- Expiry from `ATS:EmailAccountOtpExpiryInMinutes`, defaulting to 10.
- Attempts capped at 5, mirroring Auth's `RegisterService`. Without a cap, a six-digit code is
  a million cheap guesses.
- `IsUsed` is set on success **and** on running out of attempts, so a consumed code cannot be
  retried. Rows outlive their use so a replay is recognised as *already consumed* rather than
  as merely missing, which would be indistinguishable from expired.
- Codes are scoped by `Purpose` (`Register` / `Edit` / `Delete`) as well as by account, so a
  code minted to approve a password change cannot be replayed against the delete endpoint.

ATS cannot reuse Auth's `OtpVerification` table: the two modules are separate `DbContext`s
against separate schemas, so that entity is not reachable from here. `ats."EmailAccountOtp"` is
its shape minus the registration fields.

### Editing

Only four fields can invalidate the proof a previous code gave: **email address, password, SMTP
host, SMTP port**. Changing any of them re-runs the OTP flow; the account reverts to `Pending`
and leaves rotation until re-verified. Priority, display name, daily limit and the active flag
save immediately with no code.

The pending credentials are held as JSON on the OTP row (`PendingChangesJson`), **not** written
to the account. Writing the new password to the account first would put an unproven credential
one status change away from rotation. The JSON carries an already-protected password, and is
masked by `AtsAuditRedactor` like any other sensitive field.

### Deleting

Also requires a code to the account's own mailbox. Deleting a sender is as consequential as
adding one — the remaining accounts absorb its volume, and if it was the only one, the queue
stops.

### The in-use guard

`SmtpAccountPoolRegistry.Lease(accountId)` marks an account busy for the duration of one send,
and `IsLeased` reports it. Edit and delete throw `ConflictException` during that window — a few
seconds — because swapping credentials underneath an in-flight send either fails it or, worse,
sends it from the wrong mailbox. The read DTO carries the flag so the table can show an "In
use" badge that explains the refusal rather than presenting it as an error.

Leases are **counted, not a flag**: an account sends several messages concurrently, and a flag
would be cleared by the first one to finish while the others were still in flight.

---

## 4. Choosing an account

`SmtpAccountPoolRegistry.GetNextSendableAccountAsync` returns the **lowest `Priority` number**
that satisfies all four of:

```csharp
IsActive && IsVerified && !IsCoolingDown(now) && RemainingInWindow > 0
```

(`AtsEmailAccountSnapshot.IsSendable`.) `Priority` is **uniquely indexed**, so "the next
account" is never ambiguous — a shared priority would make failover depend on row order, which
is neither stable nor testable.

`excludedAccountIds` carries the accounts already tried **for this message**. Without it the
switcher would be handed straight back the account that just refused.

`DateTime.UtcNow` is evaluated **once** per selection, so every candidate is judged against the
same instant. Comparing each account to its own clock read would let a cooldown expiring
mid-loop make the answer depend on evaluation order.

### The quota check happens *before* the send

That ordering is the entire value of counting consumption. Reacting to the provider's refusal
is too late: a Gmail that has answered `5.4.5 Daily user sending limit exceeded` is locked for
roughly 24 hours. Moving one message early costs nothing.

`DailySendLimit` defaults to **450**, under Gmail's ~500, for the same reason. It is per account
because a Workspace mailbox allows ~2,000 and a consumer one ~500, and both may be registered in
the same table.

### Consumption is a rolling 24 hours, counted from a log

There is deliberately **no counter column**. A counter cannot express a rolling window — it
would need resetting at some fixed hour, and Google does not enforce its limit at a fixed hour:
a send at 23:00 still counts against you at 22:00 the next day. A counter reset at midnight
would report headroom that does not exist.

Instead `ats."EmailSendLog"` gets one row per successful send, and the figure is:

```sql
SELECT COALESCE(SUM("RecipientCount"), 0)
FROM ats."EmailSendLog"
WHERE "AtsEmailAccountId" = $1 AND "SentAt" >= now() - interval '24 hours';
```

Three properties follow from that, all of them load-bearing:

- **It sums recipients, not rows.** Google counts recipients. One invitation is one recipient
  today, but a future CC would consume more quota than a row count reports.
- **Only successful sends are logged.** A refused message consumed no quota, and logging it
  would make the account look more consumed than it is and retire it early.
- **The table and the routing decision read the same number.** The `312 / 450` in the UI is the
  same `SUM` the selector used, so they can never disagree.

The subquery is correlated into `GetSnapshotsAsync` so the whole list costs **one** round trip.
N+1 here would put N database calls on the critical path of every message.

`AtsEmailSendLogRetentionService` sweeps rows hourly past
`max(QuotaWindowHours + 1, SendLogRetentionHours)` — 48 hours by default. The retention window
is wider than the quota window on purpose: trimming at exactly 24 hours would race the counting
query and could subtract consumption an account genuinely used, which reads as free capacity and
walks straight into the provider's cap.

---

## 5. When the switcher moves to the next account

This is the table to read before changing anything in `ReportFailureAsync` or
`CanRetryOnAnotherAccount`.

| What happened | Counts toward the breaker | Moves **this message** | Effect on the account |
|---|---|---|---|
| Daily quota reached | pre-emptive | **before the send is attempted** | skipped by the selector until the window rolls |
| `Throttled` — `421`, `454`, "try again later" | **no** — bypasses the counter | **yes, first occurrence** | cools down `ThrottleBackoffSeconds` (10 min) |
| `Permanent`, sender-shaped — `535` bad credentials, "account suspended", "daily user sending limit exceeded" | **no** — bypasses the counter | **yes, first occurrence** | `NeedsReverification`, out of rotation until a human fixes it |
| `Permanent`, recipient-shaped — `550 no such mailbox` | **never** | **no** | none; the next message goes down the same connection |
| `Transient` — socket drop, timeout, non-throttle 4xx | **+1** | **no** (see below) | at 3 in a row: cools down `TransientFailureCooldownSeconds` (15 min) |
| Success | resets the counter to 0 | — | `LastSentAt` updated, `LastFailureReason` cleared |

**Any success resets the consecutive counter to zero.** Three failures separated by a success
never add up to a trip.

### Why three, and why a throttle does not wait for three

Three, because one dropped socket is noise and three in a row is a pattern. Waiting for ten
would spend ten messages' worth of latency discovering what the third already told us.

A throttle is not counted at all, because the provider has *already* stated this account is
sending too fast. A second opinion costs another message against a closed door. Same for a
rejected credential: waiting fixes nothing a password change would not.

### Why a `550` must never count — the most important rule here

A `550 no such mailbox` is about the **candidate's address**. It carries no information about
your Gmail. If it counted, then one bulk upload with three typo'd addresses would trip the
first account's breaker, then the second's, then the third's — and within minutes every
registered sender would be out of rotation with nothing whatsoever wrong with any of them. The
queue would stop, the logs would show "every account cooling down", and the actual cause would
be three misspelled email addresses in a spreadsheet.

This is enforced in two places, and both are needed:

- `SmtpFailureClassifier.ClassifySendFailure` sets `EmailFailureScope.Message` for a 5xx unless
  `LooksLikeSenderRejection` matches the wording. That helper is **deliberately narrow** — each
  phrase in it is one a provider uses to say "this mailbox may not send", not "that mailbox does
  not exist". Widening it to anything resembling "5xx looks serious" reintroduces the bug.
- `SmtpAccountPoolRegistry.ReportFailureAsync` returns immediately when `!result.IsAccountFault`.

### Why a transient counts but does not move the message

This is the subtlest rule on the page, and it looks like a bug until you know the incident
behind it.

A transient — socket drop, timeout — **can fire after the provider already accepted the
message**. That is documented in `ats-email-delivery.md` as the reason one candidate received
the same invitation several times, and it is why `SendTimeoutSeconds` is 60 rather than 10.

So the transient still counts against the account's breaker, and can still take that account out
of rotation for *subsequent* messages. But **this** message is not re-sent through another
account in the same call, because doing so would turn a rare duplicate into a reliable one. It
goes back to the caller to be deferred and retried on a later pass, which is what the row's
attempt budget is for.

`EmailDeliveryResult.CanRetryOnAnotherAccount` is therefore narrower than `IsAccountFault`:

```csharp
public bool CanRetryOnAnotherAccount =>
    Scope == EmailFailureScope.Account
    && Outcome is EmailDeliveryOutcome.Throttled or EmailDeliveryOutcome.Permanent;
```

Widening it to include `Transient` would look like an improvement and would duplicate
invitations.

### Two counters that are easy to confuse

| Counter | Belongs to | Threshold | What it does |
|---|---|---|---|
| `ConsumedInWindow` vs `DailySendLimit` | the **account** | 450 | **Proactive.** Skips the account before it is refused |
| `ConsecutiveFailureCount` | the **account** | 3 | **Reactive.** Retires an account that is already failing |
| `EmailSendAttempts` | the **row** (one candidate) | 5 | How many times one invitation is re-claimed before it is given up on |

The third one is not part of this feature and must never be spent on an account problem. That is
why an exhausted pass releases rows with `ReleaseEmailInvitationClaimsAsync`, which does not
increment it.

### When every account is exhausted

`SendATSEmailWithResultAsync` returns **`Throttled`**, always — never the last account's real
outcome — carrying the last failure's message text for the log.

That looks like lost information and is the opposite. `Throttled` is the only outcome meaning
"defer this row without charging an attempt". Three accounts with expired app passwords each
answer `Permanent`; returning that verbatim would have the processor retire perfectly valid
candidate addresses because *we* misconfigured something.

The processor then:

1. cancels the pass so the remaining rows do not each walk the same empty list,
2. releases them to `Pending` with their attempt count untouched,
3. raises `AtsNotificationType.EmailAccountsExhausted` to the ATS administrators **once per
   pass**, linking to `/s&i/ats/emailaccounts`.

That notification is the only thing standing between this feature and a silent outage. A capped
account defers rows correctly and quietly: the queue looks calm, the logs look normal, and
invitations stop going out. Somebody has to be told.

Administrators are resolved **by role** (`PlatformManager`, `Admin`) rather than by who holds
module 16. Module 16 is in no seeded role's grant list, so a module-based query returns nobody on
a fresh database — and the notification would go unsent precisely when it matters most.

---

## 6. Where the state actually lives

A recurring question when changing this code: does the switcher go to the database on every
message? Partly, and the split is deliberate.

| Lives in the database | Lives in process memory |
|---|---|
| The account list and its priorities | The authenticated SMTP sessions (`SmtpConnectionPool`) |
| Rolling-24h consumption (`SUM` over the send log) | The send-rate and login token buckets (`SmtpRateLimiter`) |
| `ConsecutiveFailureCount`, `CoolingDownUntil`, `LastFailureReason` | The decrypted app passwords, inside each pool |
| `VerificationStatus`, `VerifiedAt` | Live lease counts (`IsLeased`) |

Selection reads the database on every message. That cost is accepted and is exactly why the
repository is uncached: a cached view of which account is healthy is worse than no view at all,
because it routes messages to a mailbox that is already cooling down.

Health is **written through** rather than kept only in memory, because a process restart must not
resurrect an account the provider is still throttling, and the management table has to show
health without reaching into process memory. The rule is: *the database is the authority on
restart; memory is the authority while running.* A throttle is recorded in both — in memory so
the in-flight pass stops using that account immediately, on the row so the next process knows.

`CoolingDownUntil` is deliberately **not cleared by a success**. A send can complete on a
connection opened before a throttle was recorded, and letting that success cancel the back-off
would put the account straight back into a rate limit it is already in. Cooling down expires on
its own clock.

Contexts are built **on demand**, not at start-up: opening a session costs a login, and an
account that is registered but never selected should not spend one. Creation is serialised per
account, because two concurrent sends to the same new account would otherwise each build a pool
and the loser's would be dropped with its sessions still open — a login spent for nothing, which
is the resource this whole design protects.

---

## 7. Password protection at rest

The app password must be handed to the SMTP server in plaintext at `AUTH` time, so it cannot be
hashed. `IHashService` (SHA-512, one-way) is used here **only** for the OTP codes.

`AesGcmSecretProtector` implements `ISecretProtector` with AES-256-GCM over a key from
`Security:SecretProtectionKey`. GCM rather than CBC because it authenticates: a ciphertext that
has been altered, or moved to a different row, fails loudly instead of decrypting to garbage
that is then handed to an SMTP server.

The **context** (additional authenticated data) is built by `AtsEmailAccountSecrets.PasswordContext`
and binds the ciphertext to its mailbox, lower-cased:

```text
ats.EmailAccounts.EncryptedPassword:ops@cibi.com
```

Bound to the mailbox rather than to the primary key, because the key is generated by the
database — binding to it would mean inserting the row, reading the id back, and updating the
password in a second write, leaving a window where the row exists with no usable password. And
the mailbox is already one of the four fields whose change forces re-verification, so a rename
can never leave a ciphertext bound to a context that no longer exists.

### Two leak paths, both closed

- **The audit trail.** `AtsAuditRedactor.SensitivePropertyNames` includes `EncryptedPassword`,
  `AppPassword` and `SmtpPassword`. It matches **by whole name**, so `"Password"` does not cover
  them — each had to be added explicitly, and any new sensitive field must be too. Nothing else
  in the pipeline will notice that it leaked.
- **The API surface.** No DTO carries the password in any form. The read DTO is projected from
  `AtsEmailAccountSnapshot`, which has **no password field at all** — that makes "the API cannot
  leak the password" a property of the type rather than a rule someone has to remember.

### Operational consequences

- `Security:SecretProtectionKey` must be a base64 **32-byte** key, supplied as a real environment
  variable. `AesGcmSecretProtector` validates it in its constructor, so a missing or malformed
  key fails at **start-up** rather than at the first send hours later.
- **Rotating that key makes every stored SMTP password unreadable.** They must be re-entered and
  re-verified. `SmtpAccountPoolRegistry` catches the `CryptographicException` and turns it into
  an account-scoped `Permanent` — so the switcher moves on rather than retiring the recipient —
  but the account itself is out until a human fixes it.
- The seeded account is protected with the same key at seed time, so a key change breaks that one
  too.

---

## 8. Seeding and deployment

`ATSInitialData.GetPrimaryEmailAccount()` seeds the existing `Email:ATSGmail` account as
`Priority = 1`, `VerificationStatus = Verified`, applied by `ATSDatabaseExtensions` **only when
the table is empty**.

Without it the migration would land an empty table, the selector would find nothing sendable, and
every queued invitation would defer until somebody registered an account by hand — a silent
outage caused by shipping the schema.

This is the one place `Verified` is granted without a code. It is justified because these exact
credentials are what the queue has been sending through in production: requiring a code here
would retire a working sender to prove something its own delivery history already has.

The emptiness guard (rather than a guard on the address) means an operator who deliberately
deletes this account does not get it back on the next restart.

Migration: `20260911135048_AddAtsEmailAccountsATSMigration`.

---

## 9. How to verify it

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS"
dotnet build 1CibiPlatform.sln
```

Five files in `Test/Test/BackendAPI/Modules/ATS.UnitTests/` cover this feature, split by the
question each one answers:

| File | Question |
|---|---|
| `AtsEmailAccountSelectorTests` | which account carries the next message |
| `AtsEmailAccountBreakerTests` | when a failure takes an account out of rotation |
| `AtsEmailFailoverTests` | the walk from one account to the next, within one message |
| `AtsEmailAccountOtpTests` | what a code does, and what it refuses to do |
| `AtsEmailAccountManagementServiceTests` | register, edit, delete, and the in-use guard |

They share two fixtures. `AtsEmailAccountFixture` builds a **real** `SmtpAccountPoolRegistry` over
a mocked repository — the registry is the thing under test, so mocking it would test nothing.
`AtsEmailAccountManagementFixture` uses a reversible fake protector and a fake hasher rather than
mocks, so protect→unprotect and hash→verify actually round trip; a mock returning a constant would
make a context mismatch look correct.

The tests that pin the rules most likely to be "fixed" wrongly later:

- `ReportFailureAsync_ShouldNotCountARecipientRejection` — §5's most important rule. It asserts a
  `550` never reaches `RecordHealthAsync` **at all**, not merely that it did not trip the breaker.
- `EditAsync_ShouldParkTheChange_RatherThanWritingItToTheAccount` — an unproven credential stays on
  the OTP row. If this breaks, a typo'd password is one status change away from the queue.
- `SendATSEmailWithResultAsync_ShouldReportThrottled_WhenEveryAccountRefused` — a run of *account*
  failures must not be reported verbatim. Three expired app passwords produce a `Permanent`, and
  returning it would retire valid candidate addresses over our own misconfiguration.
- `ShouldSkipTheWholePass_WhenEveryAccountIsUnavailable` — the pass stands down only when the
  registry returns nothing, and raises `EmailAccountsExhausted` when it does.
- `ShouldStillRunThePass_WhenOnlySomeAccountsAreUnavailable` — one capped account does **not**
  stop the pass. If this ever starts passing for the wrong reason, the switcher is dead.
- `SmtpFailureClassifierTests` — a `454` must leave `SessionIsUsable = true` (incident 2).

One thing the unit tests deliberately do **not** prove: that the protected password is unreadable.
The fake protector is reversible by construction, so `EditAsync_ShouldParkTheChange` asserts the
protector was *applied* (the parked JSON carries an `EncryptedPassword`, never the raw
`AppPassword` field) rather than that the output is opaque. Opacity is AES-GCM's property, checked
by eye in the audit query in step 6 below.

### End to end, by hand

1. Register an account with a **wrong** app password. Expect an immediate, specific SMTP auth
   error on the form and no usable account — the row exists but stays `Pending`.
2. Register a correct one. The code arrives **in that mailbox, sent from itself**. Verify it; the
   account shows Active.
3. Set `DailySendLimit` to 2 on the primary, queue 5 invitations. Watch the pass switch after two
   sends. The Consumed column should read `2 / 2` and `3 / 450`.
4. Disable every account and queue one invitation. The row stays `Pending` with
   `EmailSendAttempts` **unchanged**, and the exhausted-accounts notification appears in the bell.
5. Edit a priority — saves with no code. Edit the password — code required, and the account
   leaves rotation until verified.
6. After a password edit, confirm no password value appears:
   ```sql
   SELECT "Payload" FROM ats."AtsAuditEntry" ORDER BY "CreatedAt" DESC LIMIT 5;
   ```

### Reading the state

```sql
-- Who is sendable right now, and how much headroom each has
SELECT a."AtsEmailAccountId", a."EmailAddress", a."Priority", a."IsActive",
       a."VerificationStatus", a."CoolingDownUntil", a."ConsecutiveFailureCount",
       a."DailySendLimit",
       COALESCE(SUM(l."RecipientCount"), 0) AS consumed_24h,
       a."LastFailureReason"
FROM ats."EmailAccounts" a
LEFT JOIN ats."EmailSendLog" l
  ON l."AtsEmailAccountId" = a."AtsEmailAccountId"
 AND l."SentAt" >= now() - interval '24 hours'
GROUP BY a."AtsEmailAccountId"
ORDER BY a."Priority";
```

`LastFailureReason` keeps the provider's own words, which is what tells an operator to raise a
limit rather than re-enter a password.

---

## 10. What not to do

- **Do not count a `Message`-scoped failure against an account.** A `550` is about the
  candidate's address. Counting it burns every registered sender on one batch of typos and leaves
  the queue with nowhere to go — see §5.
- **Do not widen `LooksLikeSenderRejection`.** Every phrase in it is one a provider uses to say
  "this mailbox may not send". "5xx looks serious" is not that, and adding it puts recipient
  failures back in the account bucket.
- **Do not add `Transient` to `CanRetryOnAnotherAccount`.** It looks like a missed chance to keep
  the message moving. It is the duplicate-invitation bug.
- **Do not return the last account's real outcome when every account is exhausted.** Only
  `Throttled` defers a row without charging an attempt. Anything else retires valid candidate
  addresses over our own misconfiguration.
- **Do not cache this repository.** Health and consumption change on every send, and this is read
  on the send hot path. A cache routes messages to an account that is already capped.
- **Do not put a send-count column on the account row.** It cannot express a rolling window, and
  the reset it would need is the failure mode Google's enforcement is designed around.
- **Do not log refused sends to `EmailSendLog`.** They consumed no quota; logging them retires the
  account early.
- **Do not clear `CoolingDownUntil` on a successful send.** That success may be riding a
  connection opened before the throttle was recorded.
- **Do not let a health write set `VerificationStatus` to `Verified`.** `RecordHealthAsync` only
  ever sets it, never clears it — granting verification is the OTP flow's job, and a health write
  must not be able to put an unproven credential back in rotation.
- **Do not drop the unique index on `Priority`.** A tie makes failover depend on row order:
  unstable in production, untestable anywhere.
- **Do not filter or order after the projection in `BuildSnapshotQuery`.** EF cannot translate a
  comparison over the whole `AtsEmailAccountSnapshot` constructor, and it fails at **runtime**,
  not at compile time. The `shape` delegate exists to apply both to the entity first.
- **Do not verify a new account by sending through the failover path.** Use
  `SendWithCredentialsAsync`. Succeeding through a *different* account would mark this one
  verified on the strength of another mailbox's password.
- **Do not add a sensitive field without adding its exact property name to
  `AtsAuditRedactor.SensitivePropertyNames`.** Matching is by whole name, and nothing warns you.
- **Do not delete or edit an account without the lease check.** Swapping credentials under an
  in-flight send either fails it or sends it from the wrong mailbox.

---

## 11. Known limitations

### The rate is per account, the volume is per account too — but only one of them helps

Registering a second mailbox roughly doubles the **daily volume** the queue can carry. It does
not make any single account send faster; `MaxSendsPerSecond` is a per-mailbox rate and each
account gets its own budget of it. If invitations are going out too slowly rather than stopping
outright, the answer is a transactional provider, not another Gmail. See
`docs/ats-email-delivery.md` §4.

### Replicas multiply the rate, not the quota

The token buckets and the in-memory cooldown are per process, so N containers send at N × the
configured rate for each account. The **quota** is not affected — it is a `SUM` in PostgreSQL
that every replica reads. Before running replicas, see the clustering note in
`ats-email-delivery.md` §7.

### An account that is cooling down is invisible to the pass, not to the operator

A cooldown is deliberately silent: it is self-clearing and normal. Only total exhaustion raises a
notification. An account that keeps cooling down and recovering will therefore not announce
itself — the Consumed column and `LastFailureReason` in the table are where that shows up.

### The breaker counter is read-then-write

`ReportFailureAsync` reads the snapshot and writes `count + 1` rather than issuing an atomic
increment. Two concurrent failures can lose one count, delaying a trip by a single message. This
is accepted: the row read is needed anyway to decide whether the threshold was crossed, and a
one-message delay in retiring an already-failing account costs less than the contention an atomic
path would add to the send hot path.
