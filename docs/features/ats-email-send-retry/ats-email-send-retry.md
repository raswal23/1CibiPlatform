# ATS email send retry

## 1. What it does

One shared attempt loop, `SingleEmailSendRetry`, used by every ATS email send that retries. It
calls a send up to three times while — and only while — the fault is transient, waiting 2s then 4s
between attempts, and returns the last result either way.

It is not a new delivery mechanism. Account failover, pacing, quotas and the circuit breaker all
already existed in `ATSEmailService.SendATSEmailWithResultAsync` — "the switcher" — and are
documented in [`ats-email-delivery`](../ats-email-delivery/ats-email-delivery.md). What this adds is
the answer to a narrower question: when the switcher has walked every account and still failed, does
the caller ask again?

## 2. Why it exists

Before it, two different things were true of the same module.

A **queued** invitation retried, because `BulkEmailNotificationProcessorService` had — and still
has — its own attempt loop: a `for`, a `switch`, and a `Task.Delay`.

An **inline** send did not. The withdrawal notice, the completion notice, the dispute
acknowledgement and the single-order invitation each called the switcher once, logged whatever came
back, and moved on. A dropped socket on the one attempt they got meant the requestor was never told
their candidate had withdrawn — and unlike a queued row, there was no later tick to put it right.
These sends run inside a request, often inside a transaction; the pass that would retry them does
not exist.

Copying the queue's loop into each of the four files would have made four copies of one rule, in a
module where the rule is subtle enough to have its own incident history. So the inline sends got one
shared helper instead.

**The queue was deliberately left alone.** Its loop stays where it is, and this helper is not used
there. Two things make the queue's retry a different rule rather than the same one spelled twice:

- It **re-reads the pass's stand-down signal before every attempt**. One row discovering that no
  account will take a message must stop its siblings from each spending a full budget finding out
  the same thing. A helper that wraps only the send has no way to consult a signal between attempts.
- A spent budget there is **not the end of the road**. The row is released and the next tick picks it
  up, so those attempts pace one pass rather than bounding a message. An inline notice has no next
  tick, so its attempts are all it gets.

They read separate options for that reason — `MaxAttemptsPerPass` for the queue,
`MaxAttemptsPerMessage` here — so tuning a bulk pass's pacing cannot quietly change a withdrawal
notice's only chance at delivery.

## 3. How it works

```text
SingleEmailSendRetry.SendAsync(send, maxAttempts, baseDelaySeconds, logger, description, ct)
  attempt 1..3:
    result = await send(attempt)
    Sent                -> return
    Permanent / Message -> return        (refused recipient)
    Throttled           -> return        (every account has already refused)
    Transient           -> sleep 2s, then 4s, and ask again
  budget spent          -> return the last Transient
```

**The nesting is the design.** The helper wraps the switcher from the *outside*, so one attempt is
one full walk of the registered accounts:

```text
SingleEmailSendRetry   3 attempts on ONE message
  └─ the switcher      walks every account, within a single attempt
       └─ one SMTP conversation per account
```

The budget is therefore per **message**, not per account. Registering a sixth account buys quota and
failover; it does not turn three attempts into eighteen.

### Why `Transient` is the only outcome retried

This is not a second policy layered on top of the switcher. It falls out of the switcher's own exits.

| What came back | Why asking again is wrong |
|---|---|
| `Throttled` | It only reaches the helper once **every** account has refused. The switcher moved between accounts itself and never reported the first throttle upward. `SmtpAccountPoolRegistry` cools an account down on its first throttle precisely because a second knock on a closed door costs another message against a provider that has already said "slow down" |
| `Permanent` scoped to the message | The server read the address and refused it. A second attempt produces the identical refusal, and so does another account |
| `Transient` | The switcher deliberately does **not** move a transient to another account — a timeout can fire after the provider already accepted the message, which is how a candidate once received the same invitation twice. So it arrives as the final answer with the highest-priority account still selected |

A retried transient therefore lands on that same account again — unless its consecutive failures
have meanwhile tripped `ConsecutiveFailureThreshold` and retired it, in which case the next attempt
walks on to the account behind it. Stay put while it looks like noise, move on once it looks like a
pattern, and let the breaker rather than this loop decide which it is.

### The accepted risk

Retrying a transient can duplicate a message the provider already accepted. That is the same hazard
`CanRetryOnAnotherAccount` refuses to take *across* accounts, taken deliberately in place.

A duplicate was judged cheaper than a requestor never hearing that their candidate withdrew. The
bound on the damage is that the budget is three and does not scale with the account list, and that
the back-off puts 2s and 4s in front of each retry rather than re-sending in the same breath.

## 4. The five call sites

| Send | Where | On a spent budget |
|---|---|---|
| Application form invitation | `EndorsementSubmissionService.SendApplicationFormToUserEmailAsync` (`bool` overload) | Returns false to the caller, inside the transaction |
| Application form reminder | the same method, `isFollowUp: true` | as above |
| Withdrawal notice | `WithdrawnEmailNotification` | Logged and swallowed |
| Completion notice | `SubmittedFormEmailNotification` | Logged and swallowed |
| Dispute acknowledgement | `DisputeEmailNotification` | Logged and swallowed |

All five are inline sends with no later tick, which is what they have in common and what the queue
does not have. That is why `AtsEmailDeliveryOptions` names this budget `MaxAttemptsPerMessage`, and
why the queue reads `MaxAttemptsPerPass` instead.

`BulkEmailNotificationProcessorService.SendWithRetryAsync` is **not** a call site. It keeps its own
loop — see §2.

Only the send goes inside the retry. Composing the body, resolving the requestor's mailbox and
building the copy list happen once, above it. Attempt 2 re-sends the message; it does not rebuild it.

## 5. Configuration

Both bounds come from `AtsEmailDeliveryOptions`, supplied by the caller — the helper is static and
reads no configuration itself.

| Key | Default | Meaning |
|---|---|---|
| `MaxAttemptsPerMessage` | 3 | Attempts on one message. Floored at 1: a configured `0` must not mean "never send" |
| `RetryBaseDelaySeconds` | 2 | First back-off, doubling per attempt — so 2s, then 4s |

No `appsettings.*.json` in this repository binds an `AtsEmailDelivery` section, so every deployment
currently runs these defaults.

Worst case added latency is ~6s per message, and it does not grow with the number of registered
accounts, because the only outcome that sleeps is also the only one that does not switch.

## 6. How to verify it

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~SingleEmailSendRetry"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS.UnitTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS.IntegrationTests"
```

Manually, which is the only way to see a successful send: point the highest-priority account at a
port that drops the connection, leave a second account healthy, and withdraw an application. Expect
three attempts in the log roughly 2s and 4s apart, no switch to the second account, and one
`WithdrawalNoticeEmail` history row. Then set the first account's daily limit to 0 and repeat:
expect no retry at all and an immediate switch.

## 7. What not to do

- **Do not wrap a send without checking what is already beneath it.** Nested budgets multiply, they
  do not share. A retry on `SendApplicationFormToUserEmailWithResultAsync` would cost a queued row
  nine sends per pass, because `SendWithRetryAsync` already loops over that exact method. The retry
  on the single-order path sits on the `bool` overload for that reason, and the two overloads stay
  separate for no other.
- **Do not widen the predicate past `Transient`.** For `Throttled` it re-knocks on a closed door; for
  `Permanent` it re-sends to an address the server has already refused. Both spend a message against
  a provider's quota to learn something already known.
- **Do not put body composition, a directory lookup or a copy list inside the `send` lambda.** It
  runs up to three times.
- **Do not replace the queue's loop with this helper.** It was tried and reverted: the queue needs
  to re-read its stand-down signal between attempts, and its budget bounds a pass rather than a
  message. Sharing the code would tie two different rules to one number.
- **Do not make the helper read `IOptions` itself.** Static and stateless is what lets the attempt
  loop be tested with scripted results and no SMTP server.

## 8. Known limitations

- **A retried transient can duplicate a delivered message** (§3). Accepted deliberately; the
  alternative was a notice that silently never arrives.
- **The queue's cancellation paths are still unguarded.** A cancellation from either
  `semaphore.WaitAsync` or `SendWithRetryAsync`'s `Task.Delay` escapes into `Task.WhenAll` and skips
  the pass's status writes. The fix is to write the status batches in a `finally` — see
  `ats-email-delivery_code_explanation.md` §10.1. This helper is not on the queue's path and does not
  change that either way.
- **Attempts are not visible on the order.** `EmailSendAttempts` counts passes, not attempts within
  one. Three transient attempts on an inline notice leave nothing but log lines; a requestor asking
  "was I emailed?" is answered from the history row, which records the attempt, not the outcome.
