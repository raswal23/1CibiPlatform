# ATS email send retry — code explanation

Companion to [`ats-email-send-retry.md`](./ats-email-send-retry.md). That document says what the
retry is and why. This one is the call chain, file by file, with the code that actually ships.

Assumes [`ats-email-delivery_code_explanation.md`](../ats-email-delivery/ats-email-delivery_code_explanation.md),
which covers the switcher, the pool, the limiter, the breaker and the queue. This document does not
repeat them; it covers only the loop wrapped around them and the six places that call it.

---

## 1. The helper

`BackendAPI/Modules/ATS/Services/EmailService/SingleEmailSendRetry.cs`. One `public static class`,
one method, no state and no injected configuration.

```csharp
	public static async Task<EmailDeliveryResult> SendAsync(
		Func<int, Task<EmailDeliveryResult>> send,
		int maxAttempts,
		int baseDelaySeconds,
		ILogger logger,
		string description,
		CancellationToken cancellationToken)
	{
		var attemptCeiling = Math.Max(1, maxAttempts);

		for (var attempt = 1; ; attempt++)
		{
			var result = await send(attempt);

			// Sent, a refused recipient, or an exhausted account rotation all leave immediately.
			// Only a transient is worth sending twice.
			if (result.Outcome != EmailDeliveryOutcome.Transient || attempt >= attemptCeiling)
			{
				return result;
			}

			var backoff = TimeSpan.FromSeconds(baseDelaySeconds * Math.Pow(2, attempt - 1));

			logger.LogWarning(
				"Attempt {Attempt} of {MaxAttempts} to send {Description} failed transiently: {StatusCode} {Message}. Retrying in {BackoffSeconds}s.",
				attempt,
				attemptCeiling,
				description,
				result.StatusCode,
				result.Message,
				backoff.TotalSeconds);

			await Task.Delay(backoff, cancellationToken);
		}
	}
```

Five things in that method are deliberate.

**`for (var attempt = 1; ; attempt++)` has no bound in its header.** The predicate below owns both
exits. A bound in the header would be a second place to get the off-by-one wrong, and the two would
then have to agree about whether the last attempt sleeps before returning. It does not: the
`return` fires before the back-off, so three attempts means two delays — 2s and 4s, not 2s, 4s and a
pointless third.

**`Math.Max(1, maxAttempts)`.** A configured `0` must not mean "never send". The floor is applied
once, to a local, so the log line reports the same ceiling the loop is using.

**The predicate is one line.** `result.Outcome != Transient || attempt >= attemptCeiling`. Written
as a `switch` it would have three cases that all do the same thing, and the reader would have to
check that they really do.

**`send` takes the attempt number.** Callers that want "(attempt 2)" in a log line get it without
the helper knowing anything about their message. The queue uses it — `attempt == 1 ? null : attempt`
— and the five inline sends discard it with `_ =>`.

**The `Task.Delay` is not wrapped in a `try`.** Cancellation semantics belong to the caller, and the
two kinds of caller want opposite things. The queue catches it and releases the row; an inline
caller lets it propagate, because a cancelled HTTP request has no row to release.

### 1.1 What a single attempt actually covers

The helper wraps `ATSEmailService.SendATSEmailWithResultAsync` — the switcher — from the **outside**.
Three loops end up nested, and which one is which is the whole design:

```text
SingleEmailSendRetry.SendAsync          attempt 1..3, on ONE message
  └─ SendATSEmailWithResultAsync        while (true) over the accounts, priority order
       └─ SendThroughAccountAsync       one account: lease, pace, connect, send, classify
```

So an attempt is a full walk of every registered account, and the budget is per **message**. This is
why `AtsEmailDeliveryOptions.MaxAttemptsPerMessage` is named for the message rather than the
account or the pass — three attempts stay three attempts when a sixth account is registered.

### 1.2 Why only `Transient` survives the predicate

Not a second policy. It is what is left after the switcher's own exits, which are at
`ATSEmailService.cs:113`:

```csharp
			if (result.IsSent || !result.CanRetryOnAnotherAccount)
			{
				return result;
			}
```

and `CanRetryOnAnotherAccount` is:

```csharp
	public bool CanRetryOnAnotherAccount =>
		Scope == EmailFailureScope.Account
		&& Outcome is EmailDeliveryOutcome.Throttled or EmailDeliveryOutcome.Permanent;
```

Reading the two together, outcome by outcome:

| Reaches the helper as | Because the switcher… | Retrying would… |
|---|---|---|
| `Sent` | succeeded | — |
| `Permanent` / `Message` | returned on the first account; another would refuse the same address identically | re-send to a mailbox the server has already read and rejected |
| `Throttled` | exhausted **every** account and synthesised this — it never reports the first throttle upward, it moves on instead | knock again on a door that is closed, which `SmtpAccountPoolRegistry.ReportFailureAsync` avoids by cooling an account on its *first* throttle |
| `Transient` | returned immediately, because `CanRetryOnAnotherAccount` excludes it, leaving `attemptedAccountIds` empty | ask again — the one case worth it |

The `Transient` exclusion is load-bearing and predates this work: a timeout can fire *after* the
provider accepted the message, which is how a candidate once received the same invitation twice.
`EmailDeliveryResult.cs`'s own `<remarks>` records that incident.

**Where attempt 2 lands.** It re-enters the switcher with an empty `attemptedAccountIds`, so
selection starts from the highest-priority sendable account — usually the same one it just failed
on. Usually, not always: that transient counted toward the account's `ConsecutiveFailureThreshold`,
and if it was the third, the breaker has retired the account and attempt 2 walks to the next. That
is intended. Stay put while it looks like noise, move on once it looks like a pattern, and let the
breaker decide which it is.

---

## 2. The five call sites

### 2.1 The three order notices

`WithdrawnEmailNotification`, `SubmittedFormEmailNotification`, `DisputeEmailNotification` — all the
same shape. From `WithdrawnEmailNotification.cs:111`:

```csharp
		// The body, the mailbox and the copy list are all resolved above and stay resolved: only the
		// send is inside the retry, so a second attempt re-sends the same message rather than
		// rebuilding it and charging the directory another lookup.
		var result = await SingleEmailSendRetry.SendAsync(
			send: _ => _emailSender.SendATSEmailWithResultAsync(
				toEmail: requestor.UserEmail,
				subject: WithdrawnEmail.Subject,
				body: body,
				cancellationToken: cancellationToken,
				cc: cc),
			maxAttempts: _options.MaxAttemptsPerMessage,
			baseDelaySeconds: _options.RetryBaseDelaySeconds,
			logger: _logger,
			description: $"the withdrawal notice for order {invitation.EmailInvitationID}",
			cancellationToken: cancellationToken);
```

**Only the send is in the lambda.** `BuildWithdrawnApplicationNotification`, the `IAuthQueries`
lookup that resolves the requestor's mailbox, and the `cc` list are all built above it and captured.
Attempt 2 re-sends the same message; it does not rebuild it or charge the directory a second lookup.
`SendAsync`'s XML doc states this as a contract on the `send` parameter, because nothing in the type
system enforces it.

**Each of the three took a new constructor parameter** — `IOptions<AtsEmailDeliveryOptions> options`,
stored as `_options` — purely to supply the two bounds. Nothing else in these classes reads it.

**Failure handling is unchanged.** The result is logged and swallowed; a dead SMTP account must not
reach `CustomExceptionHandler` and answer a committed withdrawal or a filed dispute with a 500,
which would invite the user to do it again. The order-history row is written **before** the outcome
is inspected, so a failed delivery still records that the attempt happened.

The three differ only in the `description` string:

| Class | `description` |
|---|---|
| `WithdrawnEmailNotification` | `the withdrawal notice for order {id}` |
| `SubmittedFormEmailNotification` | `the completion notice for order {id}` |
| `DisputeEmailNotification` | `the dispute acknowledgement for order {id}` |

### 2.2 `EndorsementSubmissionService` — the overload split, which is now load-bearing

Two overloads reach the same send. Which one carries the retry decides whether a queued row costs
three sends or nine.

```text
EndorsementSubmissionService.SendApplicationFormToUserEmailAsync  (bool)      <- RETRY HERE
  └─ SendApplicationFormToUserEmailWithResultAsync                (result)    <- NOT here
       └─ ATSEmailService.SendATSEmailWithResultAsync             (switcher)

BulkEmailNotificationProcessorService.SendWithRetryAsync                      <- ITS OWN LOOP
  └─ TrySendEmailAsync
       └─ SendApplicationFormToUserEmailWithResultAsync            (result)   <- shared
```

The `bool` overload is the **single-order** path — one caller, `EndorsementSubmissionService.cs:215`,
inside `InsertEmailInvitationRequestAsync`, which runs inside a `TransactionRunner`. The result
overload is what the **queue** calls, at `BulkEmailNotificationProcessorService.cs:454`.

`:357`, quoted in full because it is the only thing preventing the nesting bug:

```csharp
	/// <remarks>
	/// The retry lives on THIS overload rather than on the result-aware one below, and that is the
	/// whole reason the two are still separate.
	///
	/// This is the single-order path: it runs inline inside a transaction, so a failed send takes
	/// the order with it and there is no later tick to try again on. It spends the message's
	/// attempt budget here or not at all.
	///
	/// The overload below is what the queue calls, and
	/// <c>BulkEmailNotificationProcessorService</c> already loops over it in its own
	/// <c>SendWithRetryAsync</c>. Putting the retry there instead would nest one inside the other
	/// and cost a queued row nine sends rather than three.
	/// </remarks>
	public async Task<bool> SendApplicationFormToUserEmailAsync(...)
	{
		var result = await SingleEmailSendRetry.SendAsync(
			send: _ => SendApplicationFormToUserEmailWithResultAsync(
				gmail, name, applicationFormLink, requestor, requestorId, clientId, CancellationToken.None),
			maxAttempts: _emailDeliveryOptions.MaxAttemptsPerMessage,
			baseDelaySeconds: _emailDeliveryOptions.RetryBaseDelaySeconds,
			logger: _logger,
			description: $"the application form invitation to {gmail}",
			cancellationToken: CancellationToken.None);
```

**Budgets multiply, they do not share.** A retry on the result overload would sit *inside* the
queue's retry: 3 × 3 = 9 sends per pass, × 5 passes = 45 SMTP attempts for one persistently
transient address. With the split, every path is exactly three per pass.

**Two sends, one call site.** Invitation and reminder are the same method — `isFollowUp` selects the
subject and body — so wrapping it once covers both. That is why the feature is five sends in four
files.

`CancellationToken.None` is pre-existing on this path (`ATSEmailService.cs:27` does the same) and is
untouched here. Its consequence for the retry is that the back-off cannot be cancelled, so a
shutdown mid-retry waits out up to 6s.

### 2.3 The queue is NOT a call site — `BulkEmailNotificationProcessorService.SendWithRetryAsync`

Replacing the queue's loop with this helper was tried and **reverted**. The queue keeps its own
`for`, `switch` and `Task.Delay`, untouched by this feature. Two properties of a queued row make its
retry a different rule rather than the same one spelled twice:

```csharp
		for (int attempt = 1; attempt <= maxAttempts; attempt++)
		{
			if (throttleSignal.IsCancellationRequested)
			{
				return EmailDeliveryOutcome.Throttled;
			}

			var result = await TrySendEmailAsync(request, attempt == 1 ? null : attempt, cancellationToken);
			...
```

**The stand-down signal is re-read before every attempt.** A pass fans out across a semaphore, and
any one row discovering that no account will take a message cancels `throttleSignal` for all of them.
Checking it only once — which is all a helper wrapping the send can do — lets every sibling already
past that point spend its full budget rediscovering the same fact. `SingleEmailSendRetry` has no
parameter for "a condition to re-check between attempts", and adding one would make it a worse helper
for the five callers that have no such condition.

**The budget bounds a pass, not a message.** Exhausting it releases the row; the next tick picks it up
with `MaxEmailSendAttempts` passes still available. Exhausting the inline budget is the end of the
road. That is why they read separate options — `MaxAttemptsPerPass` here, `MaxAttemptsPerMessage`
there — and why collapsing them would tie a bulk pass's pacing to a withdrawal notice's only chance at
delivery.

`attempt == 1 ? null : attempt` is passed into `TrySendEmailAsync` so a first attempt logs without an
attempt number and a retry logs with one. That parameter exists for this loop alone.

**Still open here:** the `Task.Delay` takes the linked token, so a stand-down raised by another row's
task throws out of this method, faults `await Task.WhenAll(sendTasks)`, and skips every status write
below — including `UpdateBulkEmailInvitationRequestForSentEmailAsync(successList)`, which strands
already-sent rows in `Processing` until the 30-minute sweeper flips them back to `Pending` and the
next pass sends them again. That is `ats-email-delivery_code_explanation.md` §10.1, unchanged by this
feature. The fix is to write the status batches in a `finally`, not to move the loop.

---

## 3. Configuration

`Configuration/AtsEmailDeliveryOptions.cs`. **Two** attempt options, deliberately not one:

```csharp
	// Attempts within one pass, before the row goes back to the queue for a later tick.
	public int MaxAttemptsPerPass { get; set; } = 3;

	// Attempts for ONE message by a caller sending it right now, before the sender gives up on it
	// entirely. Each attempt is a full walk of the registered accounts, because account failover
	// is the switcher's job and happens inside a single attempt.
	public int MaxAttemptsPerMessage { get; set; } = 3;
```

Same default, different meaning. `MaxAttemptsPerPass` is read only by the queue's own loop, which
releases the row when it runs out. `MaxAttemptsPerMessage` is read only by this helper, whose callers
have no later tick. They are equal today, and nothing should assume they stay equal — that is the
point of the split.

`RetryBaseDelaySeconds = 2` is shared by both: 2s then 4s.

Verified by grep at the time of writing: **no `appsettings.*.json` in this repository binds an
`AtsEmailDelivery` section**, so neither name could silently change a deployed value.

---

## 4. Tests

`SingleEmailSendRetry` is static and takes a delegate, which is the entire reason it is a separate
class: the attempt loop can be driven with scripted results and no SMTP server.

At the five call sites, the tests that matter are the ones pinning that a failure does not escape —
`…ShouldNotThrow_WhenTheSenderThrows` and `…ShouldNotThrow_WhenEverySenderAccountRefuses` in each of
the three notice test classes. `EverySenderAccountRefuses` stubs `Throttled`, which the predicate
returns on immediately, so it still asserts `Times.Once` and still asserts the cc list.

Each of the five affected test classes supplies the bounds explicitly:

```csharp
			Options.Create(new AtsEmailDeliveryOptions
			{
				MaxAttemptsPerMessage = MaxAttempts,
				RetryBaseDelaySeconds = 0
			}));
```

`RetryBaseDelaySeconds = 0` is not cosmetic. The production default sleeps 2s then 4s, and a test
that drives a failing send through three attempts would otherwise add six seconds to the suite each
time.

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS.UnitTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS.IntegrationTests"
```

---

## 5. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| The predicate in `SendAsync` | All five call sites, and §1.2 | Widening past `Transient` re-knocks on a closed door for `Throttled` and re-sends to a refused address for `Permanent`. The queue has its own equivalent `switch` and does **not** follow this one |
| `SendAsync`'s signature | Five call sites; four are positional-looking named-argument blocks | The compiler catches it, but the `description` strings are free text and will silently go stale |
| Where a retry wraps a send | Whether one already exists beneath it | Budgets multiply. `EndorsementSubmissionService`'s overload split is the live instance (§2.2) |
| `EmailDeliveryResult.CanRetryOnAnotherAccount` | This document §1.2, plus `ats-email-delivery_code_explanation.md` §3.4 | It decides what reaches the retry at all. Admitting `Transient` would make the switcher move accounts on a fault the retry is meant to keep in place |
| `MaxAttemptsPerMessage` | `ats-email-delivery.md` §4's table | This is the whole ceiling on the inline path — there is no second pass to multiply it by. The "15 attempts" arithmetic in the code explanation belongs to the queue and uses `MaxAttemptsPerPass` |
| `MaxAttemptsPerPass`, or any move to merge it with `MaxAttemptsPerMessage` | §2.3 | They bound different things. A queued row has a next tick; an inline notice does not |
| The queue's `SendWithRetryAsync` | `ats-email-delivery_code_explanation.md` §1.5 and §10.1 | It is not this helper and never was. Its stand-down re-check is load-bearing |
| Anything inside a `send:` lambda | That it is still only the send | Body composition or a directory lookup in there runs up to three times |

---

## When to update this document

Whenever a seventh call site appears, the predicate changes, the overload split in
`EndorsementSubmissionService` changes, or either bound moves. A new call site is the most likely
edit and the easiest to get wrong — check §2.2 before adding one.
