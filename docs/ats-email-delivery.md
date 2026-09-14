# ATS Email Delivery

How invitation emails reach candidates without tripping the provider's rate limits, and why
the design looks the way it does.

Related: `docs/ats-email-accounts.md` (the registry of sender accounts this page sends
through, and how one is registered), `docs/ats-notifications.md` (what raises the
notifications this job completes), `docs/feature-development-guide.md`.

---

## 1. What it does

`EmailNotificationBackgroundJob` polls every 5 seconds, claims a slice of `Pending`
invitations from PostgreSQL, and emails each candidate their application-form link. It is
the step between "a bulk upload was parsed into orders" and "the candidates have been asked
for their details".

The hard part is not sending. It is sending **a few hundred messages through a consumer mail
provider** without being throttled, and without emailing anyone twice.

## 2. Two incidents, two different limits

This design is the result of two failures. They look similar in the logs and have opposite
fixes, so the distinction matters more than anything else on this page.

### Incident 1 — the send limit (14 messages)

A bulk upload sent **14 emails and stopped**. Three faults combined:

**A login per message.** `ATSEmailService` built a `System.Net.Mail.SmtpClient` per email:
connect, TLS, `AUTH LOGIN`, one message, disconnect. 200 invitations meant **200 logins**.
The wall arrived at 14 messages in about eight seconds — roughly 1.75 logins/second.

**Every failure looked identical.** The send returned `bool`, so a `550 no such mailbox`
(permanent) and a `421 try again later` (back off now) were both retried immediately. The
budget multiplied: 3 attempts per pass × 5 claim rounds = **up to 15 attempts per address**,
all aimed at a provider already saying no.

**A 10-second timeout on an accepted message.** `Timeout = 10000` fired while the provider
had *already accepted* the message. The code recorded failure and retried, so candidates
received duplicates. That is why "rate limited" and "sent twice" appeared together.

### Incident 2 — the login limit (`454 Too many login attempts`)

The first fix introduced pooling, and pooling on the happy path worked. But **every failure
discarded the session**, and a discarded session forces a fresh login. So:

1. A send fails → session discarded
2. Next send opens a **new connection** and authenticates
3. Provider: `454 Too many login attempts` — because you just logged in again
4. Session discarded → back to step 2

**The recovery mechanism was generating the error it was reacting to.** Worse, `454` is
raised by `AuthenticateAsync`, inside `AcquireAsync` — it never passed through the send
path's `try/catch`, so it was reported as a generic transient fault and retried, opening yet
another connection. The throttle detection never fired.

The lesson: **volume and authentication are separate budgets.** Pacing messages does nothing
for a login throttle.

## 3. How it works now

```text
EmailNotificationBackgroundJob (Quartz, every 5s, DisallowConcurrentExecution)
  -> EmailNotificationProcessorService.ProcessForPendingStatusAsync
       -> [skip entirely if NO registered account is sendable]
       -> claim slice from PostgreSQL (FOR UPDATE SKIP LOCKED)
       -> per row: SendApplicationFormToUserEmailWithResultAsync
            -> ATSEmailService.SendATSEmailWithResultAsync   <- the switcher loop
                 -> registry.GetNextSendableAccountAsync(already tried)
                 -> [no account left? return Throttled, defer the row]
                 -> SendThroughAccountAsync(accountId)
                      -> registry.GetContextAsync  (that account's pool + limiter)
                      -> registry.Lease(accountId) (blocks edit/delete mid-send)
                      -> SmtpRateLimiter.WaitForSlotAsync        (paces MESSAGES)
                      -> SmtpConnectionPool.AcquireAsync
                           -> reuse an idle session, or:
                           -> refuse if IsLoginThrottled
                           -> SmtpRateLimiter.WaitForLoginSlotAsync  (paces LOGINS)
                           -> connect + authenticate, classified on failure
                      -> MailKit SmtpClient.SendAsync
                      -> classify: outcome + SCOPE + whether the SESSION survives
                      -> registry.ReportSuccessAsync / ReportFailureAsync
                 -> [CanRetryOnAnotherAccount? loop to the next account]
       -> write Sent / Error, release Deferred
```

### Three bounds, deliberately separate — now per account

| Component | Bounds | Default | Scope |
|---|---|---|---|
| `SmtpConnectionPool` | Concurrent SMTP **sessions** | 2 | one per account |
| `SmtpRateLimiter.WaitForSlotAsync` | **Messages** per second | 0.9 | one per account |
| `SmtpRateLimiter.WaitForLoginSlotAsync` | Seconds between **logins** | 5 | one per account |

Keeping these apart is the central decision. The pool hides latency. The send limiter keeps
you under the volume ceiling. The login limiter keeps you under the *authentication* ceiling
— a budget the other two cannot see.

Because a limiter cannot be outrun by concurrency, **raising the connection count cannot
raise either rate**, so tuning for speed can never re-create either incident.

**They are still singletons, but the sender is no longer the process.** All three bound
resources belonging to *one sending mailbox*, and there are now several registered mailboxes
(see `docs/ats-email-accounts.md`). `SmtpAccountPoolRegistry` — itself a singleton — holds one
`(pool, limiter)` pair per account id, built on first use and disposed when that account is
edited or deleted. Every rule below still holds *within* an account; only the word "sender"
narrowed from "this process" to "this mailbox".

The limits are deliberately **not** shared across accounts. A provider budgets per mailbox, so
one shared 0.9/s across two Gmails would halve the throughput that registering a second Gmail
exists to buy.

A per-scope pool is still not a pool, and a per-scope limiter would still let two concurrent
passes each run at full rate. That is why the registry is a singleton and takes
`IServiceScopeFactory` rather than injecting `ATSDBContext` — a singleton cannot hold a scoped,
non-thread-safe `DbContext`.

### A failed send does not discard the session

`SmtpFailureClassifier.ClassifySendFailure` returns `SessionIsUsable` alongside the outcome:

| Response | Session survives? | Why |
|---|---|---|
| `550` recipient rejected | **Yes** | About the mailbox, not the connection |
| `454` throttled | **Yes** | The socket is still open |
| `421` service closing | No | The server has hung up |
| Protocol error / socket drop | No | Connection is not trustworthy |

This table is the fix for incident 2. Discarding on every failure is what turned one
throttle into a stream of logins.

### Failures are classified where they happen

`ClassifyConnectFailure` runs **inside the pool**, so a throttle raised during
connect/authenticate is recognised rather than escaping as an unclassified exception. It
surfaces as `SmtpConnectFailedException` (already classified) or
`SmtpLoginThrottledException` (the pool declined to try at all).

| Outcome | Trigger | What happens |
|---|---|---|
| `Sent` | Server accepted | Row marked `Done` |
| `Permanent` | 5xx, bad credentials | Fails on the **first attempt** — no retries |
| `Transient` | 4xx, socket drop, timeout | Retried with exponential back-off |
| `Throttled` | 421, 454, "try again later" | **This account stops**; the message moves on |

### Every result also carries a *scope*

The outcome says what the server decided. `EmailFailureScope` says **who it was about**, and
that second axis is what makes multi-account sending possible at all.

| Scope | Meaning | Example |
|---|---|---|
| `Message` | About this recipient. The account is fine. | `550 no such mailbox` |
| `Account` | About the sending mailbox. Says nothing about the recipient. | `454`, `421`, `535 bad credentials` |

Only `Account`-scoped failures reach the breaker. Counting a `550` would let one bulk upload
of typo'd addresses retire every registered sender in minutes — the queue would have nowhere
left to send, with nothing actually wrong. That rule lives in
`SmtpAccountPoolRegistry.ReportFailureAsync` and is the one most likely to be "simplified"
wrongly later; `docs/ats-email-accounts.md` has the full breaker table.

### A throttle stops one account, not the pass

When a send or login comes back `Throttled`:

1. That account's `SmtpRateLimiter.ReportThrottled` parks **it** —
   `ThrottleBackoffSeconds` (10 min) for a send throttle, `LoginThrottleBackoffSeconds`
   (30 min) for a login throttle, because auth limits are enforced over a wider window. The
   same cooldown is written through to `CoolingDownUntil` on the row, so a restart cannot
   readmit an account the provider is still throttling.
2. The switcher asks the registry for the next account that has not already refused **this
   message** and sends it there, inside the same call. A throttle is refused before the body
   is accepted, so re-sending delivers the invitation exactly once.
3. Only when *every* registered account has refused does `SendATSEmailWithResultAsync` return
   `Throttled`. At that point a `CancellationTokenSource` signals every queued task in the
   pass to stand down — the remaining rows would walk the same empty list to the same answer.
4. Those rows go back to `Pending` via `ReleaseEmailInvitationClaimsAsync`, which
   deliberately **does not increment `EmailSendAttempts`**. They were never accepted by a
   server, so charging them an attempt would retire a valid address after five throttles
   without a single real delivery failure.
5. `RaiseAccountsExhaustedAsync` notifies the ATS administrators once per pass, not once per
   row. Otherwise a single outage buries the bell under hundreds of identical entries.
6. Subsequent ticks call `HasSendableAccountAsync` **before claiming anything** and return
   immediately while the answer is no.

**A transient is the exception, and it is deliberate.** A socket drop or a timeout counts
against the account's breaker, but the *message* does not move to another account — it is
deferred to a later pass instead. A timeout can fire after the provider already accepted the
message (that is how a candidate received the same invitation twice, in incident 1), so
resending it elsewhere in the same breath would turn a rare duplicate into a reliable one.
The predicate is `EmailDeliveryResult.CanRetryOnAnotherAccount`, which is narrower than
`IsAccountFault` for exactly this reason.

### Exhaustion is reported as `Throttled`, never as the last real failure

When the switcher runs out of accounts it returns `Throttled` with the last account's message
text — it does **not** return that account's own outcome.

This looks like lost information and is the opposite. `Throttled` is the only outcome meaning
"defer this row without charging an attempt". Three accounts whose app passwords have expired
each answer `Permanent`; returning that verbatim would have the processor retire perfectly
valid candidate addresses on the strength of our own misconfiguration. The provider's wording
still travels in the message, because "raise the daily limit" and "the password is wrong" need
very different responses from whoever reads the log.

## 4. Configuration

Bind `AtsEmailDelivery` in appsettings to override any value. Every default works, so an
absent section is valid — the same convention as `AtsNotifications` and `AtsAudit`.

| Key | Default | Meaning |
|---|---|---|
| `MaxConcurrentConnections` | 2 | Simultaneous SMTP sessions |
| `MaxSendsPerSecond` | 0.9 | Global message rate |
| `MinSecondsBetweenLogins` | 5 | Minimum gap between new sessions |
| `MaxMessagesPerConnection` | 50 | Messages before a session is rebuilt |
| `SendTimeoutSeconds` | 60 | Network timeout per operation |
| `MaxAttemptsPerPass` | 3 | Attempts before requeueing a transient failure |
| `RetryBaseDelaySeconds` | 2 | First back-off; doubles per attempt |
| `ThrottleBackoffSeconds` | 600 | Pause after a send throttle |
| `LoginThrottleBackoffSeconds` | 1800 | Pause after a login throttle |
| `ConsecutiveFailureThreshold` | 3 | Transients in a row before an account leaves rotation |
| `TransientFailureCooldownSeconds` | 900 | How long a tripped breaker keeps it out |
| `DefaultDailySendLimit` | 450 | Applied to a new account when none is given |
| `QuotaWindowHours` | 24 | Window the consumption figure is counted over |
| `SendLogRetentionHours` | 48 | How long a send-log row is kept |

The last five belong to the account registry; they are documented in full, with the reasoning
for each number, in `docs/ats-email-accounts.md`.

The first group's defaults come from the incidents: the provider accepted ~1.75
messages/second before refusing, so 0.9 is about half the observed ceiling. That clears 200
invitations in roughly **3.7 minutes** — slower per message than the old burst, but it
finishes, which the burst did not. Each registered account gets that rate of its own, so two
accounts clear the same 200 in about half the time.

### Tuning for a faster provider

`smtp.gmail.com` with an app password is a consumer endpoint (~500 recipients/day free,
~2,000 on Workspace). It is not built for bulk. Moving to `smtp-relay.gmail.com` or a
transactional provider (SES, SendGrid, Postmark) is the real answer for volume. Raise
`MaxSendsPerSecond` first and `MaxConcurrentConnections` second, in steps, watching for
`421`s.

Note the difference between the two ceilings. `MaxSendsPerSecond` is a **rate** — how fast one
mailbox may send — and no number of accounts raises it for any single account. `DailySendLimit`
is a **volume**, and that one *does* scale with the number of registered accounts: two verified
Gmails give the queue roughly 900 recipients a day instead of 450. Registering another account
is the cheap fix for volume; it is not a fix for rate.

## 5. How to verify it

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~Smtp"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~EmailNotificationProcessorServiceTests"
dotnet build 1CibiPlatform.sln
```

`SmtpFailureClassifierTests` pins the incident-2 regression directly: a `454` must leave
`SessionIsUsable = true`. If that assertion is ever "fixed" to `false`, the re-login loop is
back.

`SmtpRateLimiterTests` asserts the property that matters for incident 1: eight concurrent
callers are still paced at the global rate, so concurrency cannot outrun the limit.

### In a live environment

The per-pass summary is the first thing to read:

```text
Email processing completed in 12.4s. Success: 11, Failed: 0, Deferred: 0
```

`Deferred > 0` now means something stronger than it used to: **every registered account
refused**, not just one. A single throttled Gmail no longer defers anything — the switcher
moves the message and the count stays at zero. Sustained `Deferred` means either
`MaxSendsPerSecond` is still too high on every account, or the accounts are genuinely capped
and another one needs registering.

The switch itself is logged one line above it:

```text
Sender account 1 (ats@cibi.com) could not carry a message to x@y.com: 454 Too many
login attempts. Trying the next account.
```

A steady trickle of these is the feature working. A flood of them for one account id means
that account needs attention — read `LastFailureReason` in the Email Accounts table.

**The login count is the leading indicator:**

```text
Opened SMTP session to smtp.gmail.com:587 as ... Logins this process: 2.
```

Over a whole run this should stay close to `MaxConcurrentConnections`. If it climbs with the
message count, the pool is not pooling and a `454` is coming.

To confirm queue health:

```sql
SELECT "EmailSentStatus", "EmailSendAttempts", count(*)
FROM ats."EmailInvitationRequest"
GROUP BY 1, 2 ORDER BY 1, 2;
```

Rows at `Error` with `EmailSendAttempts = 5` are the fingerprint of the original bug. They
should no longer appear from throttling alone.

## 6. What not to do

- **Do not discard a session because a send failed.** Only `421` and transport faults end a
  connection. This is the single most important invariant here — breaking it re-creates the
  `454` loop, where each recovery attempt causes the next failure.
- **Do not add concurrency to go faster.** Each account's limiter bounds that account
  globally; more connections send no more messages per second. Raise `MaxSendsPerSecond`, or
  register another account.
- **Do not make the pool or the limiter scoped, and do not share one set across accounts.**
  They bound an account-wide resource. A scoped pool re-authenticates per operation, which is
  the original bug; a shared limiter makes two mailboxes split one mailbox's budget.
- **Do not resolve `SmtpConnectionPool` or `SmtpRateLimiter` from DI.** They are no longer
  registered, because there is no longer exactly one of each. Ask
  `ISmtpAccountPoolRegistry.GetContextAsync` for an account's pair.
- **Do not count a `Message`-scoped failure against an account.** A `550` is about the
  candidate's address. Counting it burns every registered sender on one batch of typos and
  leaves the queue with nowhere to go.
- **Do not widen `CanRetryOnAnotherAccount` to cover transients.** It looks like a missed
  opportunity to keep the message moving. It is the duplicate-invitation bug: a transient can
  fire after the provider accepted the message.
- **Do not classify SMTP failures anywhere but `SmtpFailureClassifier`.** A throttle raised
  during authentication has to be recognised at the point the login happens; that is exactly
  what the first version missed.
- **Do not lower `SendTimeoutSeconds` back toward 10.** A timeout that fires after the
  provider accepted the message is recorded as a failure and retried — that is how the same
  candidate gets emailed twice.
- **Do not charge an attempt for a deferred row.** `ReleaseEmailInvitationClaimsAsync`
  exists specifically to avoid it.
- **Do not retry a permanent rejection.** A 5xx is the server stating a fact.
- **Do not shorten the Quartz interval to increase throughput.** It is a poll interval; the
  rate limiter is the real bound.
- **Do not cache the account list or its health.** `AtsEmailAccountRepository` is uncached on
  purpose: a cached view of which account is healthy routes messages to a mailbox that is
  already cooling down.

## 7. Known limitations

### Clustered schedulers share one identity

`ATSServiceConfiguration` sets `q.SchedulerId = "ATS"` with `UseClustering()`. Quartz
clustering expects a **unique instance id per node** (`AUTO`); a fixed id means every
container claims the same identity, so `qrtz_scheduler_state` holds one row no matter how
many run.

The limiters are per-process *per account*, so **N containers send at N × the configured rate
for each account**. With one deployment per environment this is correct today. Before running
replicas, either set `SchedulerId = "AUTO"` and divide the rates by the replica count, or move
the limiters behind a shared store.

The quota counter does not have this problem: it is a `SUM` over `ats."EmailSendLog"` in
PostgreSQL, so every replica reads the same consumption figure. Only the in-memory halves — the
rate buckets and the live cooldown — are per process, and the cooldown is written through to the
row, so a second replica learns about it on its next selection rather than never.

### A permanent rejection still costs five passes

The claim query re-selects rows where `EmailSentStatus = 'Error' AND EmailSendAttempts < 5`.
It cannot tell a 5xx from a 4xx — it sees only `Error` and a count. So a `550` fails fast
*within* a pass (1 attempt instead of 3) but is still re-claimed on four later passes.

The clean fix is a distinct terminal status (`EmailStatus.Rejected`) excluded from that
`WHERE`. It touches the status enum, the UI status pill and the bulk dashboard counts, so it
has not been done.

## 8. Operator-forced retry (resend)

`ResendApplicationFormAsync` **queues** rather than sending. This matters, and it used to
work the other way.

The old version sent inline, on the request thread, and wrote only the token fields. Three
bugs followed:

1. It **bypassed the pool and the rate limiter entirely** — opening its own SMTP session per
   click, which is the per-message login that caused incident 1.
2. It left `EmailSentStatus` and `EmailSendAttempts` untouched, so a **successful** resend
   still displayed as `Error` / `5 attempts`. The operator saw "failed 5 times" for mail the
   candidate had just received.
3. Because the row never became `Done`, `RaiseForCompletedBulkEmailsAsync` could never mark
   that file complete — the "All 40 of 40 invitations sent" notification never fired for any
   bulk upload containing a resent row.

And the failure the user actually reported: an exhausted row (`Error` / `attempts = 5`) was
re-queued in name only. The claim query re-claims `Error` rows *only while attempts are under
the ceiling*, so nothing picked it up. The email sometimes went out (inline) and sometimes
did not (when the inline send failed), and either way the status never moved.

`RequeueEmailInvitationAsync` fixes all of it in one statement — the same shape as
`RequeueExhaustedTicketAsync` on the ticketing side:

| Field | Set to | Why |
|---|---|---|
| `EmailSentStatus` | `Pending` | Back on the queue; the job delivers it |
| `EmailSendAttempts` | `0` | Otherwise the claim query skips it — **this was the bug** |
| `EmailClaimedAt`, `EmailSentAt` | `null` | No stale claim or send timestamp |
| `HashToken` + expiry | reissued | A queued row never carries an unannounced token |
| `OrderStatus`, `ApplicationFormStatus` | `Pending` | The candidate has something to do again |

**The status predicate is the concurrency guard.** It lives inside the `UPDATE`, so a
read-then-write race cannot resurrect a live claim. Only `Processing` is excluded — that row
is mid-send right now, a worker is about to write its outcome, and the message may already be
in flight. `Pending` is deliberately allowed: nothing has been sent, so re-queueing
duplicates nothing, and refusing would give an operator a confusing error for clicking twice.

Zero rows updated means the button was stale, and the service raises `ConflictException`
rather than reporting a silent success.

Both boards also offer a **bulk** requeue over a multi-select, sharing this same statement
and its guarantees. See `docs/ats-bulk-requeue.md` for the batch cap, per-row scope
enforcement, and why a partly-stale selection is reported rather than rejected.

### What not to do here

- **Do not send inline from the resend path.** It bypasses both limiters and re-creates the
  login burst. Queue it.
- **Do not requeue without resetting `EmailSendAttempts`.** The row will sit at the ceiling
  and never be claimed — the retry looks like it worked and nothing happens.
- **Do not allow a requeue while `Processing`.** That races the worker's status write.
