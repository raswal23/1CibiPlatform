# ATS Email Delivery — Code Explanation

Companion to [`ats-email-delivery.md`](ats-email-delivery.md). That document explains *what* the
delivery path does and *why* the two throttles exist. This one exists so a developer can change
the implementation without opening every file cold — it walks the real call chains, names the
exact method at each hop, and quotes the code that carries the correctness.

Read it top to bottom once, then use it as a map: *"I'm changing X, what else touches it?"* is
answered by §12.

> **Read §0 first.** The design doc is mostly accurate but is now wrong or incomplete in twelve
> places, one of which (C1) means a login throttle is durable for ten minutes rather than the
> thirty the doc promises. Every claim below was verified against the code on branch
> `feature/Update-ReadMe-File`; where the two disagree, this document follows the code.

**Scope boundary.** The *registry of sender accounts* — `SmtpAccountPoolRegistry`, its per-account
context cache, leasing, the breaker writes, `GetNextSendableAccountAsync`, quota and password
protection — is documented in
[`ats-email-accounts_code_explanation.md`](../ats-email-accounts/ats-email-accounts_code_explanation.md)
§4. This document cross-references those sections instead of repeating them, and covers the
delivery path: the Quartz job, the claim query, the pacing mechanism, failure classification, the
retry budget, and the resend slices.

---

## 0. Where the design doc no longer matches the code

| # | `ats-email-delivery.md` says | The code actually does |
|---|---|---|
| **C1** | §3 step 1: a login throttle parks the account for `LoginThrottleBackoffSeconds` (30 min) and "the same cooldown is written through to `CoolingDownUntil` on the row, so a restart cannot readmit an account the provider is still throttling" | The 30 minutes reaches **only the in-memory limiter** (`SmtpConnectionPool.cs:157`). The row is written with `ThrottleBackoffSeconds` (10 min) for *every* throttle, send or login — `SmtpAccountPoolRegistry.cs:229` has no branch on where the throttle came from. See §10.3 |
| **C2** | §4 config table: `MaxSendsPerSecond` = "Global message rate" | Per **account**. One `SmtpRateLimiter` per account id, built in `SmtpAccountPoolRegistry.BuildContextAsync`. The doc's own §3 prose says this; the table row and the comment on `AtsEmailDeliveryOptions.MaxSendsPerSecond` ("across ALL connections … enforces this globally") are both stale |
| **C3** | §2 presents "3 attempts per pass × 5 claim rounds = **up to 15 attempts per address**" as a fault that was fixed | Both multipliers are still live: `MaxAttemptsPerPass = 3` and `MaxEmailSendAttempts = 5`. What changed is that `Permanent` returns on attempt 1 and `Throttled` defers *without* charging an attempt. A persistently **transient** address still costs 15 SMTP attempts (§2.4) |
| **C4** | §1: the job "claims a slice of `Pending` invitations" | It also re-claims `Error` rows while `EmailSendAttempts < 5`. §7 of the doc says this; §1 does not |
| **C5** | §8: "`RequeueEmailInvitationAsync` fixes all of it in one statement" | The requeue is one statement, but `EndorsementSubmissionService.ResendApplicationFormAsync` then calls `_orderHistoryService.RecordAsync` in a **second** write, and this path has no `TransactionRunner` around it. A crash between the two leaves a requeued row with no history entry |
| **C6** | §8's field table lists `EmailSentStatus`, `EmailSendAttempts`, `EmailClaimedAt`, `EmailSentAt`, `HashToken` + expiry, `OrderStatus`, `ApplicationFormStatus` | It omits `HashTokenCreatedAt`, which the same `ExecuteUpdateAsync` also sets |
| **C7** | §8: "**Both boards** also offer a bulk requeue over a multi-select" | Only `BulkUploadSubjectsDialog` calls `ResendApplicationFormsAsync` (`:435`). `WithdrawnApplicationComponent` has single-row resend only (`:95`) |
| **C8** | — (the doc does not mention it) | `IAtsEmailSender.SendThroughAccountAsync`'s XML doc says it is "used to prove credentials during registration and re-verification". **No caller does.** Registration and re-verification use `SendWithCredentialsAsync`; the only caller of `SendThroughAccountAsync` is the switcher loop at `ATSEmailService.cs:106` |
| **C9** | §3 step 5: exhaustion is announced "once per pass, not once per row" | True per pass — and the pass runs every **5 seconds**. Nothing dedupes across passes, so a 10-minute cooldown raises ~120 `EmailAccountsExhausted` rows per administrator, each of which also toasts. **NOT FOUND**: any cross-pass suppression (§10.6) |
| **C10** | §8: "Do not send inline from the resend path" — implies inline sending is gone | The **single-enrolment** path still sends inline: `InsertEmailInvitationRequestAsync` calls `SendApplicationFormToUserEmailAsync` *inside* `TransactionRunner.RunAsync`. It does go through the pool and limiter, so it is not the incident-1 bypass, but it holds an open DB transaction while queued behind `WaitForSlotAsync` (§10.7) |
| **C11** | §4: "Bind `AtsEmailDelivery` in appsettings to override any value" | **NOT FOUND.** No `AtsEmailDelivery` section exists in any `appsettings*.json`, `docker-compose*.yml` or env file in the repository. Every deployment runs the compiled defaults in `AtsEmailDeliveryOptions` |
| **C12** | §4 implies every listed default applies | `SmtpRateLimiter`'s constructor falls back to the default for `MaxSendsPerSecond <= 0`, and `SmtpConnectionPool` does the same for `MaxConcurrentConnections <= 0` — but the login interval is `Math.Max(0, options.Value.MinSecondsBetweenLogins)`. A configured `0` silently disables login pacing with no fallback (§10.4) |

Two claims in the doc were verified as **accurate** and are worth recording because they are the
ones most likely to be doubted: `SmtpFailureClassifierTests` really does assert
`sessionIsUsable.Should().BeTrue()` for a `454`
(`ClassifySendFailure_ShouldKeepTheSession_WhenThrottledWithoutDisconnect`), and
`SmtpRateLimiterTests.WaitForSlotAsync_ShouldHoldTheRate_WhenCallersRunConcurrently` really does
race eight callers. §7's proposed `EmailStatus.Rejected` genuinely does not exist — `EmailStatus`
has exactly four constants (§4).

---

## 1. One invitation, end to end

This is the full trace. Everything else in the document is a diff against it or a detail of one
of its steps.

```text
Quartz trigger, every 5s  [DisallowConcurrentExecution]
  → EmailNotificationBackgroundJob.Execute                       (BackgroundJobs/EmailNotification/)
    → scope.ServiceProvider.GetRequiredService<IEmailNotificationProcessorService>()
      → EmailNotificationProcessorService.ProcessForPendingStatusAsync
        → IATSRepository.ReleaseStaleEmailInvitationClaimsAsync(30 min)
        → HasSendableAccountAsync  →  ISmtpAccountPoolRegistry.GetNextSendableAccountAsync([])
             [no sendable account? raise EmailAccountsExhausted and return]
        → IATSRepository.GetPendingEmailInvitationRequestsAsync()   ← the claim, raw SQL
        → SemaphoreSlim(MaxConcurrentConnections * 2) fan-out over the claimed slice
          → SendWithRetryAsync(request, linkedToken, throttleSignal)   up to MaxAttemptsPerPass
            → TrySendEmailAsync            ← one DI scope PER ATTEMPT
              → IEndorsementSubmissionService.SendApplicationFormToUserEmailWithResultAsync
                → ResolveClientNameAsync(clientId)                  (cosmetic, guarded)
                → ATSEmailService.SendAppplicationFormNotification  (builds the HTML body)
                → IAtsEmailSender.SendATSEmailWithResultAsync       ← the switcher loop
                  → ISmtpAccountPoolRegistry.GetNextSendableAccountAsync(alreadyTried)
                  → SendThroughAccountAsync(accountId)
                    → ISmtpAccountPoolRegistry.GetContextAsync(accountId)      [accounts §4.2]
                    → ISmtpAccountPoolRegistry.Lease(accountId)                [accounts §4.3]
                    → SendOverContextAsync(context)
                      → SmtpRateLimiter.WaitForSlotAsync            ← paces MESSAGES
                      → SmtpConnectionPool.AcquireAsync
                        → reuse an idle session, else CreateConnectionAsync
                          → refuse if SmtpRateLimiter.IsLoginThrottled
                          → SmtpRateLimiter.WaitForLoginSlotAsync   ← paces LOGINS
                          → MailKit ConnectAsync + AuthenticateAsync
                          → on failure: SmtpFailureClassifier.ClassifyConnectFailure
                      → MailKit SmtpClient.SendAsync(message)
                      → SmtpFailureClassifier.ClassifySendFailure   (+ does the session survive?)
                    → ISmtpAccountPoolRegistry.ReportSuccessAsync / ReportFailureAsync  [accounts §4.4]
                  → loop while result.CanRetryOnAnotherAccount
            → transient? Task.Delay(RetryBaseDelaySeconds * 2^(attempt-1)) and try again
        → UpdateBulkEmailInvitationRequestForSentEmailAsync(successList)      → Done
        → UpdateBulkEmailInvitationRequestForNotSentEmailAsync(errorList)     → Error, attempts+1
        → ReleaseEmailInvitationClaimsAsync(abandonedList)                    → Pending, attempts intact
        → IAtsNotificationService.RaiseForCompletedBulkEmailsAsync(attempted)
```

### 1.1 The Quartz tick — `BackgroundJobs/EmailNotification/`

The whole job class:

```csharp
[DisallowConcurrentExecution]
public class EmailNotificationBackgroundJob : IJob
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<EmailNotificationBackgroundJob> _logger;

	public EmailNotificationBackgroundJob(IServiceScopeFactory scopeFactory, ILogger<EmailNotificationBackgroundJob> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		using var loggingScope = _logger.BeginScope(new Dictionary<string, object> { ["Application"] = "ATS" });
		using var scope = _scopeFactory.CreateScope();

		var processor = scope.ServiceProvider
			.GetRequiredService<IEmailNotificationProcessorService>();

		await processor.ProcessForPendingStatusAsync(context.CancellationToken);
	}
}
```

Byte-for-byte the same shape as `OMSTicketingBackgroundJob` — one logging scope, one DI scope,
resolve the processor, call it, pass `context.CancellationToken` straight through. That last part
matters more than it looks; see §10.1.

The trigger, `EmailNotificationBackgroundJobSetup.cs`:

```csharp
		// 5 seconds, not 1. The job is [DisallowConcurrentExecution], so a trigger that
		// fires while a pass is running is skipped - the interval only decides how quickly
		// an IDLE worker notices new work. At 1s that was two queries a second forever
		// (the stale-claim UPDATE and the claiming CTE) to shave at most four seconds off
		// a delay no candidate can perceive.
		//
		// It is also not the send rate. Throughput is bounded by AtsEmailDeliveryOptions
		// (MaxSendsPerSecond), so shortening this interval polls the database harder
		// without sending a single message faster.
		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("EmailNotificationTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInSeconds(5).RepeatForever()));
```

Five seconds against the ticketing job's ten (`OMSTicketingBackgroundJobSetup.cs:13`). The
`IConfigureOptions<QuartzOptions>` shape is what lets several modules each add a job without any
of them owning the Quartz options object; registered at `ATSServiceConfiguration.cs:170`.

### 1.2 `ProcessForPendingStatusAsync` — one pass, in order

`Services/EmailNotificationProcessor/EmailNotificationProcessorService.cs`. The two constants the
design doc does not name:

```csharp
	// No account is available for THIS pass. Distinct from the per-account throttle state that
	// used to live on the single process-wide limiter: one capped Gmail must no longer stop the
	// queue, because the switcher's whole job is to carry on through the next account.
	private static readonly IReadOnlyCollection<int> NoExcludedAccounts = [];

	// Comfortably longer than a full send pass so a live worker is never robbed of rows it
	// is still processing. A pass is now bounded by the send RATE rather than by
	// concurrency: 200 messages at the default 0.9/s is roughly four minutes, and the
	// throttle back-off can add ten more.
	private static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(30);
```

Line 23. `StaleClaimTimeout` lives on the **processor**, not the repository — the same split the
OMS ticketing doc records as correction C6, and the same 30 minutes
(`OMSTicketingProcessorService.cs:7`).

The opening sequence, verbatim:

```csharp
		// A crash mid-send leaves rows claimed as Processing with no live worker, so
		// release anything stale before claiming the next slice.
		var released = await _repository.ReleaseStaleEmailInvitationClaimsAsync(StaleClaimTimeout);

		if (released > 0)
		{
			_logger.LogWarning(
				"Released {ReleasedCount} stale email invitation claim(s) back to Pending.",
				released);
		}
```

then the gate that decides whether to claim at all:

```csharp
		if (!await HasSendableAccountAsync(cancellationToken))
		{
			_logger.LogWarning(
				"Skipping email pass: every registered sender account is capped, cooling down, unverified or disabled.");

			await RaiseAccountsExhaustedAsync(cancellationToken);

			return;
		}
```

`HasSendableAccountAsync` asks the registry for an account and throws the answer away, with the
reason inline:

```csharp
	private async Task<bool> HasSendableAccountAsync(CancellationToken cancellationToken)
	{
		var account = await _poolRegistry.GetNextSendableAccountAsync(
			NoExcludedAccounts,
			cancellationToken);

		return account is not null;
	}
```

> Asks for an account and throws the answer away. Deliberate: "is one available" and
> "which one is next" must never be two pieces of logic that can disagree, because a
> selector that says yes and a send that then finds nothing would claim a slice of rows
> only to defer every one of them.

It reads the **same** `IsSendable(now)` rule the switcher does (accounts §4.1), which is what
makes the promise hold — and §10.3 is the case where the promise leaks, because `IsSendable` reads
`CoolingDownUntil` from the row while the pool reads its throttle from memory.

### 1.3 The claim query — `Data/Repository/EmailInvitations/ATSRepository.EmailInvitations.cs:21`

```csharp
	public async Task<List<EmailInvitationRequest>> GetPendingEmailInvitationRequestsAsync()
	{
		// Claim and return in one statement. FOR UPDATE SKIP LOCKED lets a concurrent
		// worker step over rows another worker is already claiming instead of blocking,
		// and the Processing write is what keeps the claim after this transaction ends.
		// EF cannot express SKIP LOCKED, so this is raw SQL.
		return await _dbcontext.EmailInvitationRequests
			.FromSqlRaw(
				"""
				WITH ranked AS (
					SELECT "EmailInvitationID",
						   ROW_NUMBER() OVER (
							   PARTITION BY "ClientId"
							   ORDER BY "OrderCreatedAt") AS rn
					FROM ats."EmailInvitationRequest"
					WHERE ("EmailSentStatus" = {2}
						OR ("EmailSentStatus" = {3} AND "EmailSendAttempts" < {4}))
				)
				UPDATE ats."EmailInvitationRequest" t
				SET "EmailSentStatus" = {0},
					"EmailClaimedAt" = {1}
				WHERE t."EmailInvitationID" IN (
					SELECT e."EmailInvitationID"
					FROM ats."EmailInvitationRequest" e
					WHERE e."EmailInvitationID" IN (
						SELECT "EmailInvitationID" FROM ranked WHERE rn <= {5})
					ORDER BY e."OrderCreatedAt"
					LIMIT {6}
					FOR UPDATE SKIP LOCKED
				)
				RETURNING t.*;
				""",
				EmailStatus.Processing,
				DateTime.UtcNow,
				EmailStatus.Pending,
				EmailStatus.Error,
				MaxEmailSendAttempts,
				PerClientSliceSize,
				200)
			.AsNoTracking()
			.ToListAsync();
	}
```

Positional binding — the argument order **is** the contract and does not match the order the
placeholders appear in the SQL:

| Placeholder | Value | Meaning |
|---|---|---|
| `{0}` | `EmailStatus.Processing` | the status being written |
| `{1}` | `DateTime.UtcNow` | claim timestamp |
| `{2}` | `EmailStatus.Pending` | claimable: never attempted |
| `{3}` | `EmailStatus.Error` | claimable: failed but still under budget |
| `{4}` | `MaxEmailSendAttempts` (5) | the retry budget, in **passes** |
| `{5}` | `PerClientSliceSize` (50) | fair-share cap per client |
| `{6}` | `200` | total batch cap — **a bare literal, not a named constant** |

Answering the question the design doc leaves open: **yes, this is the same `FOR UPDATE SKIP LOCKED`
shape as the ticketing claim**, on the inner sub-SELECT, with the durable claim being the
`EmailSentStatus` write rather than the lock. `OMSTicketingRepository.ClaimPendingTicketsAsync` was
modelled on this method and differs in the ways tabulated in §7.

The constants, lines 5-12:

```csharp
	// An invitation is retried until this many failed sends, then it stays Error for a
	// human to look at - a mistyped or dead address must not consume the daily quota
	// forever.
	private const int MaxEmailSendAttempts = 5;

	// Round-robin: each client may contribute at most this many invitations per tick, so
	// one large upload cannot block every other client behind it.
	private const int PerClientSliceSize = 50;
```

Note what is **not** here: the batch cap. `200` is an unnamed argument at line 59 while the
ticketing side names its equivalent `ClaimBatchSize = 50` (`OMSTicketingRepository.cs:21`). Grepping
for a constant will not find it.

Also note there is no terminal flag equivalent to `IsTicketed`. `EmailSentStatus` alone gates the
queue, which is why a `Done` row can be put back to `Pending` by a resend (§6) and why
`EmailStatus.Rejected` — the fix §7 of the design doc proposes — would have to be added to this
`WHERE` to stop permanent rejections being re-claimed.

The stale-claim sweeper, `:191`:

```csharp
	public async Task<int> ReleaseStaleEmailInvitationClaimsAsync(TimeSpan staleAfter)
	{
		// A crash mid-send leaves rows stuck in Processing with no live worker. Anything
		// claimed longer ago than staleAfter goes back to Pending for the next tick.
		var cutoff = DateTime.UtcNow.Subtract(staleAfter);

		return await _dbcontext.EmailInvitationRequests
			.Where(x => x.EmailSentStatus == EmailStatus.Processing
					 && x.EmailClaimedAt != null
					 && x.EmailClaimedAt < cutoff)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Pending)
				.SetProperty(x => x.EmailClaimedAt, x => (DateTime?)null));
	}
```

It does **not** charge an attempt — consistent with `ReleaseEmailInvitationClaimsAsync` (§1.10) and
with the ticketing sweeper. It also takes no `CancellationToken`. That combination is the second
half of §10.1.

### 1.4 The fan-out and the throttle signal

```csharp
		// Concurrent, not List: several sends complete at once and List<T>.Add from
		// multiple threads corrupts the backing array without throwing.
		var successBag = new ConcurrentBag<EmailInvitationRequest>();
		var errorBag = new ConcurrentBag<EmailInvitationRequest>();
		var abandonedBag = new ConcurrentBag<EmailInvitationRequest>();

		// Concurrency is now bounded by the connection pool and the send rate is bounded by
		// the limiter, so this only decides how many rows are in flight awaiting a slot.
		// It sits slightly above the connection count so a returning connection never waits
		// for a task to be scheduled.
		var inFlightLimit = Math.Max(1, _options.MaxConcurrentConnections * 2);

		using var semaphore = new SemaphoreSlim(inFlightLimit);
```

Line 95. Four by default, **derived from configuration** rather than a constant — the ticketing job
hardcodes `MaxDegreeOfParallelism = 3`. The derivation is the point: raising
`MaxConcurrentConnections` raises the in-flight window automatically, and neither raises the send
rate, because the rate lives in the limiter (§2).

```csharp
		// Trips only when EVERY account has refused. Every task still queued checks it before
		// sending and leaves its row untouched instead of knocking again.
		//
		// It no longer trips on the first throttle. That was correct with one sender - there
		// was nowhere else to go - and is wrong with several: standing the pass down because
		// the highest-priority Gmail hit its cap would leave the remaining accounts idle, which
		// is precisely the failure the switcher exists to prevent.
		using var throttleSignal = new CancellationTokenSource();

		using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken,
			throttleSignal.Token);
```

Then the per-row lambda:

```csharp
		var sendTasks = allRequests.Select(async request =>
		{
			// Checked before the semaphore as well as inside the retry loop: a pass that is
			// already abandoning work should not queue hundreds of tasks to discover that
			// one at a time.
			if (throttleSignal.IsCancellationRequested)
			{
				abandonedBag.Add(request);
				return;
			}

			await semaphore.WaitAsync(cancellationToken);

			try
			{
				var outcome = await SendWithRetryAsync(request, linkedTokenSource.Token, throttleSignal);

				switch (outcome)
				{
					case EmailDeliveryOutcome.Sent:
						successBag.Add(request);
						break;

					// Released rather than counted as a failure. The row keeps its attempt
					// budget for a pass that runs after the throttle clears - spending it
					// against a closed door is what turned a ten-minute deferral into a
					// much longer one.
					case EmailDeliveryOutcome.Throttled:
						abandonedBag.Add(request);
						break;

					default:
						errorBag.Add(request);
						break;
				}
			}
			finally
			{
				semaphore.Release();
			}
		});

		await Task.WhenAll(sendTasks);
```

Two things to notice. `semaphore.WaitAsync` at line 123 takes the **outer** token, while
`SendWithRetryAsync` gets the **linked** one — so the throttle signal cannot cancel a task waiting
for a slot, only one that is already sending. And `Task.WhenAll` at line 154 is the single point
where an escaping exception from any row aborts the whole pass, including every write below it.
That is §10.1.

### 1.5 `SendWithRetryAsync` — the in-pass retry budget

```csharp
	private async Task<EmailDeliveryOutcome> SendWithRetryAsync(
		EmailInvitationRequest request,
		CancellationToken cancellationToken,
		CancellationTokenSource throttleSignal)
	{
		var maxAttempts = Math.Max(1, _options.MaxAttemptsPerPass);

		for (int attempt = 1; attempt <= maxAttempts; attempt++)
		{
			if (throttleSignal.IsCancellationRequested)
			{
				return EmailDeliveryOutcome.Throttled;
			}

			var result = await TrySendEmailAsync(request, attempt == 1 ? null : attempt, cancellationToken);

			switch (result.Outcome)
			{
				case EmailDeliveryOutcome.Sent:
					return EmailDeliveryOutcome.Sent;

				case EmailDeliveryOutcome.Permanent:
					// Nothing about a second attempt changes an unknown mailbox.
					return EmailDeliveryOutcome.Permanent;

				case EmailDeliveryOutcome.Throttled:
					// Every account refused this message - the switcher already tried them all,
					// and each one's own cooldown was recorded against it as it did. Nothing to
					// park here; the accounts are already out of rotation and will readmit
					// themselves when their cooldowns lapse.
					//
					// Tell every queued task to stand down. The remaining rows would each walk
					// the same empty account list and reach the same answer.
					await throttleSignal.CancelAsync();

					return EmailDeliveryOutcome.Throttled;
			}

			// Exponential, not fixed: a server that is briefly unavailable needs longer than
			// two seconds, and re-knocking at a constant interval is what a provider reads
			// as a client that will not take no for an answer.
			if (attempt < maxAttempts)
			{
				var backoff = TimeSpan.FromSeconds(
					_options.RetryBaseDelaySeconds * Math.Pow(2, attempt - 1));

				await Task.Delay(backoff, cancellationToken);
			}
		}

		return EmailDeliveryOutcome.Transient;
	}
```

Line 264 is the unguarded `Task.Delay`. `cancellationToken` here is the **linked** token, so
`throttleSignal.CancelAsync()` at line 251 — called from a *different* row's task — makes this one
throw. There is no `catch` in this method and none in the caller's lambda.

The switch has three exits and a fall-through: `Sent`, `Permanent` (one attempt, no retry),
`Throttled` (stand the whole pass down), and everything else — which is only ever `Transient` —
falls out of the `switch` into the back-off. Note the outcome that reaches here as `Throttled` has
already been through the switcher, so by definition **every** registered account refused this
message; that is why standing the pass down is correct here and would have been wrong before the
switcher existed.

### 1.6 `TrySendEmailAsync` — the per-attempt scope

```csharp
		// A row with no address can never be sent, and retrying it twice more only delays
		// the pass. Permanent so it is retired rather than requeued.
		if (string.IsNullOrWhiteSpace(request.EmailAddress))
		{
			_logger.LogError(
				"Invitation has no email address and cannot be sent: {@Context}",
				logContext);

			return EmailDeliveryResult.Permanent(null, "The invitation has no email address.");
		}

		try
		{
			// One scope per attempt, resolved here rather than using an injected service.
			// IEndorsementSubmissionService is Scoped and reaches a DbContext (it looks up
			// the client name for the email body). DbContext is NOT thread-safe, so sharing
			// one instance across concurrent sends corrupts its change tracker in ways that
			// surface as unrelated errors much later.
			using var scope = _serviceScopeFactory.CreateScope();

			var submissionService = scope.ServiceProvider
				.GetRequiredService<IEndorsementSubmissionService>();

			var subjectName = $"{request.FirstName} {request.LastName}";
			var applicationFormLink = $"{_applicationformBaseUrl}/{request.HashToken}";

			return await submissionService.SendApplicationFormToUserEmailWithResultAsync(
				request.EmailAddress,
				subjectName,
				applicationFormLink,
				request.Requestor,
				request.ClientId,
				cancellationToken);
		}
```

**The scope is per *attempt*, not per order.** `OMSTicketingProcessorService.ProcessOneAsync` opens
one scope per order and reuses it for the repository, `IAuthQueries` and `IOMSTicketCreator`; this
method opens a fresh one on every retry, so a row that takes three attempts creates three scopes.
Both are correct — the requirement is only "never share a DbContext across concurrent tasks" — but
the granularity differs and it is the kind of thing that gets "harmonised" wrongly.

The two catch blocks:

```csharp
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Shutdown, or the pass standing down after a throttle. Not a delivery failure -
			// the row keeps its budget and is picked up again later.
			return EmailDeliveryResult.Throttled(null, "The send was cancelled before it completed.");
		}
		catch (Exception ex)
		{
			// Anything the sender did not already classify: a lookup failure, a malformed
			// address, a bug. Transient is the safe reading - the row retries rather than
			// being retired on one unexplained error.
			_logger.LogError(
				ex,
				"Unclassified failure sending email to {Email} (attempt {Attempt}): {@Context}",
				request.EmailAddress,
				retry ?? 1,
				logContext);

			return EmailDeliveryResult.Transient(null, ex.Message);
		}
```

The first is what makes the throttle signal safe *inside* a send. The second is deliberately broad
and is the mechanism behind §10.2: anything that escapes the sender — including a database failure
in the post-send bookkeeping — is read as a retryable delivery failure.

The application-form link is `$"{_applicationformBaseUrl}/{request.HashToken}"`, with
`_applicationformBaseUrl` read once in the constructor from `ATS:ApplicationFormBaseUrl`. The token
on the row is the **hash**, and it is what the candidate's URL carries.

### 1.7 The switcher — `ATSEmailService.SendATSEmailWithResultAsync` (`ATSEmailService.cs:61`)

The signature:

```csharp
	public async Task<EmailDeliveryResult> SendATSEmailWithResultAsync(
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken,
		IReadOnlyCollection<string>? cc = null)
```

`cc` is optional and last — C# requires optional parameters to trail required ones, and that
ordering is load-bearing rather than cosmetic: it is what let the withdrawal notice add a copy list
without touching any existing caller. `SendATSEmailAsync` below and
`EndorsementSubmissionService.SendApplicationFormToUserEmailWithResultAsync` both still compile
unchanged. See `docs/features/ats-withdrawn-application-email/`.

Its XML `<remarks>` is the authoritative statement of the three ways out, and is worth reading in
place of any summary:

```text
/// Three ways out, and the third is the subtle one:
///
/// A failure scoped to the message - a bad recipient - returns immediately. Trying a
/// different sender cannot fix an address that does not exist.
///
/// A throttle or an auth rejection moves the message to the next account. Both are refused
/// before the body is accepted, so re-sending delivers it exactly once.
///
/// A transient - socket drop, timeout - does NOT move the message, even though it counts
/// against the account. It can fire after the provider already accepted the message, so a
/// resend here would reliably duplicate an invitation that a candidate has already been
/// sent. The account may still leave rotation for subsequent messages; this one goes back
/// to the caller to defer and retry on a later pass, which is what the attempt budget is
/// for. See <c>EmailDeliveryResult.CanRetryOnAnotherAccount</c>.
///
/// Bounded by the number of registered accounts, not by a retry count: once every account
/// has refused, there is nowhere left to go and the caller must defer the row.
```

The loop itself:

```csharp
		var attemptedAccountIds = new List<int>();

		// The last account-scoped failure, returned when every account has been exhausted.
		// Reporting the real provider response beats a generic "no account available": it is
		// the difference between "raise the daily limit" and "the password is wrong".
		EmailDeliveryResult? lastAccountFailure = null;

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var account = await _poolRegistry.GetNextSendableAccountAsync(
				attemptedAccountIds,
				cancellationToken);

			if (account is null)
			{
				// Always Throttled, whatever the last account actually said.
				//
				// Throttled is the only outcome that means "defer this row WITHOUT charging an
				// attempt", and that is the correct reading here however the accounts failed:
				// nothing is wrong with the recipient. Returning the last failure verbatim
				// would be a trap - three accounts with expired app passwords produce a
				// Permanent, and the processor retires perfectly valid candidate addresses on
				// the strength of our own misconfiguration.
				//
				// The reason still travels, because "raise the daily limit" and "the password
				// is wrong" need very different responses from whoever reads the log.
				return EmailDeliveryResult.Throttled(
					lastAccountFailure?.StatusCode,
					lastAccountFailure is null
						? "Every registered sender account is capped, cooling down, unverified or disabled."
						: $"Every registered sender account refused the message. Last response: {lastAccountFailure.Message}");
			}

			attemptedAccountIds.Add(account.AtsEmailAccountId);

			var result = await SendThroughAccountAsync(
				account.AtsEmailAccountId,
				toEmail,
				subject,
				body,
				cancellationToken,
				cc);

			if (result.IsSent || !result.CanRetryOnAnotherAccount)
			{
				// Sent; or refused for a reason another account would refuse identically; or a
				// transient that may already have been delivered. The failure was still
				// reported to the breaker inside the send, so an unhealthy account still
				// leaves rotation - it just does not take this message with it.
				return result;
			}

			lastAccountFailure = result;

			_logger.LogWarning(
				"Sender account {AccountId} ({Email}) could not carry a message to {Recipient}: {StatusCode} {Message}. Trying the next account.",
				account.AtsEmailAccountId,
				account.EmailAddress,
				toEmail,
				result.StatusCode,
				result.Message);
		}
```

Three properties of this loop carry the correctness:

- **It is bounded by the account list, not by a counter.** `attemptedAccountIds` only ever grows,
  and `GetNextSendableAccountAsync` excludes it (accounts §4.1), so the loop cannot spin. There is
  no `while (true)` escape other than `account is null`, `IsSent`, or `!CanRetryOnAnotherAccount`.
- **`CanRetryOnAnotherAccount`, not `IsAccountFault`, is the continuation predicate.** That single
  choice is the double-send guard; §3.4 explains the mechanism.
- **Exhaustion returns `Throttled`, never the last real outcome.** The comment above is the whole
  argument, and it is why the processor's `Throttled` branch can mean "defer without charging an
  attempt" unconditionally.

The log line is the one the design doc §5 tells operators to watch. It fires once per abandoned
account per message, so a trickle is failover working and a flood for one account id is that
account's `LastFailureReason` waiting to be read.

### 1.8 `SendThroughAccountAsync` (`ATSEmailService.cs:134`)

```csharp
	public async Task<EmailDeliveryResult> SendThroughAccountAsync(
		int accountId,
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken,
		IReadOnlyCollection<string>? cc = null)
	{
		// Normalised once here rather than inside the message builder, so the recipient count
		// charged to this account's daily cap is provably the same list that goes on the wire.
		var copied = NormalizeRecipients(cc);

		SmtpAccountContext context;

		try
		{
			context = await _poolRegistry.GetContextAsync(accountId, cancellationToken);
		}
		catch (InvalidOperationException exception)
		{
			// The account vanished, or its stored password can no longer be decrypted. Scoped
			// to the account so the caller moves on rather than retiring the recipient.
			_logger.LogError(
				exception,
				"Could not prepare sender account {AccountId}.",
				accountId);

			return EmailDeliveryResult.Permanent(
				null,
				exception.Message,
				EmailFailureScope.Account);
		}

		// Held for the whole attempt, so an edit or delete arriving mid-send is refused rather
		// than swapping credentials underneath an open session.
		using var lease = _poolRegistry.Lease(accountId);

		var result = await SendOverContextAsync(
			context,
			toEmail,
			subject,
			body,
			cancellationToken,
			copied);

		if (result.IsSent)
		{
			// Recipients, not messages: the TO address plus everyone copied. The provider counts
			// recipients against the daily cap, so a copied message has to consume more of it -
			// see AtsEmailSendLog.RecipientCount, which is summed rather than counted.
			await _poolRegistry.ReportSuccessAsync(accountId, 1 + copied.Count, cancellationToken);
		}
		else
		{
			// Returns whether the account left rotation; the caller does not need to know,
			// because it asks the registry for the next account either way.
			await _poolRegistry.ReportFailureAsync(accountId, result, cancellationToken);
		}

		return result;
	}
```

Order matters: `GetContextAsync` **outside** the lease (building a context can spend a login, and a
failed build must not have counted as "in use" for the edit guard), then `Lease` for the whole
attempt, then the send, then the report. The `Permanent` returned for an unbuildable context is
`EmailFailureScope.Account`, so `CanRetryOnAnotherAccount` is true and the switcher moves on — and
`ReportFailureAsync` is *not* called on that path, because the `return` happens before the send.
A vanished account therefore does not trip its own breaker; the `Permanent` branch of
`ReportFailureAsync` (which sets `NeedsReverification`) is only reached for a provider rejection.

`ReportSuccessAsync(accountId, 1 + copied.Count, ...)` — the count is recipients, not messages,
matching `AtsEmailSendLog.RecipientCount` (§8). An invitation still reports `1`; the withdrawal
notice reports `3`, because it copies two addresses and the provider charges for all of them. This
call is inside no try/catch; §10.2.

### 1.9 `SendOverContextAsync` — the SMTP conversation (`ATSEmailService.cs:240`)

This private method is shared by every send path in the module: the switcher's
`SendThroughAccountAsync` and the throwaway `SendWithCredentialsAsync` (§5) both funnel into it, so
one place decides what an SMTP exception means. Accounts §1.5 documents its role in the OTP flows;
what follows is the delivery-path reading.

```csharp
		// Paced before the connection is leased. Waiting while holding a session would idle
		// a scarce resource for no reason.
		await context.RateLimiter.WaitForSlotAsync(cancellationToken);

		SmtpLease lease;

		try
		{
			lease = await context.Pool.AcquireAsync(cancellationToken);
		}
		catch (SmtpLoginThrottledException exception)
		{
			// The pool declined to open a session because authentication is rate limited.
			// Nothing was attempted, so this is reported as a throttle rather than a
			// delivery failure - the row keeps its budget.
			return exception.Result;
		}
		catch (SmtpConnectFailedException exception)
		{
			// Already classified inside the pool, including the login-throttle case that
			// used to escape unclassified and be retried into another login.
			_logger.LogWarning(
				"Could not open an SMTP session on account {AccountId} to send to {Email}: {StatusCode} {Message}",
				context.AtsEmailAccountId,
				toEmail,
				exception.Result.StatusCode,
				exception.Result.Message);

			return exception.Result;
		}
```

Note that **neither** of these two returns goes through `ReportFailureAsync` — they return straight
out of `SendOverContextAsync`, and the caller (`SendThroughAccountAsync`) does report them, because
it only checks `result.IsSent`. So a login throttle *does* reach the breaker, via the `Throttled`
branch of `ReportFailureAsync`. That is the path C1 is about.

The send and its classification:

```csharp
		await using (lease)
		{
			var message = BuildMessage(context, toEmail, subject, body, cc);

			try
			{
				await lease.Client.SendAsync(message, cancellationToken);

				lease.RecordSend();

				_logger.LogInformation("Email sent successfully to {Email}", toEmail);

				return EmailDeliveryResult.Sent;
			}
			catch (MailKit.Net.Smtp.SmtpCommandException exception)
			{
				// The server answered with a status code. The classifier also decides whether
				// the SESSION survives - a per-recipient rejection says nothing about the
				// connection, and discarding it would force a needless re-login.
				var (result, sessionIsUsable) = SmtpFailureClassifier.ClassifySendFailure(exception);

				if (!sessionIsUsable)
				{
					lease.MarkFaulted();
				}
```

`lease.RecordSend()` runs only after `SendAsync` returns, so `PooledConnection.MessagesSent` counts
accepted messages and `MaxMessagesPerConnection` retirement is driven by real traffic.

Then the three catch-block shapes that the classifier cannot see, each with its scope decision
inline. The transport one is the load-bearing comment in the whole feature:

```csharp
			catch (Exception exception) when (exception is IOException or SocketException or TimeoutException or OperationCanceledException
				&& !cancellationToken.IsCancellationRequested)
			{
				// Socket dropped or timed out. Transient, but the connection is dead.
				//
				// Note this can fire AFTER the provider accepted the message - which is exactly
				// how a candidate received the same invitation more than once. The generous
				// SendTimeoutSeconds default exists to make this rare rather than routine.
				lease.MarkFaulted();

				_logger.LogWarning(
					exception,
					"SMTP transport failure sending to {Email}. Treating as transient.",
					toEmail);

				// Account-scoped so the breaker counts it, but note that the failover loop does
				// NOT move a message on a lone transient - see the remark there. That matters
				// most here: this catch can fire after the provider already accepted the
				// message, so an immediate resend elsewhere would deliver it twice.
				return EmailDeliveryResult.Transient(
					null,
					exception.Message,
					EmailFailureScope.Account);
			}
```

Read the `when` filter carefully: C# binds `&&` tighter than `or`, so it is
`IOException or SocketException or TimeoutException or (OperationCanceledException && !cancellationToken.IsCancellationRequested)`.
A cancellation the caller actually asked for is **not** caught here — it propagates, and is picked
up by `TrySendEmailAsync`'s `OperationCanceledException` catch (§1.6). Only a cancellation that
this token did not request (a MailKit-internal one) is treated as transport noise.

`SmtpProtocolException` gets the same `Transient` + `Account` treatment with its own comment:

```csharp
				// Account-scoped: the conversation broke down, which says nothing about the
				// recipient. Scoping it to the message would leave this send pinned to a
				// connection that has already proven it cannot complete one.
```

And `BuildMessage`, which is why the context travels this far down:

```csharp
	/// <remarks>
	/// The From must match the authenticated mailbox. A message built with one account's address
	/// and pushed down another account's session is a spoof as far as the receiving server is
	/// concerned, and Gmail rejects it outright - which is why this takes the context rather than
	/// reading a configured sender.
	/// </remarks>
```

`BuildMessage` also takes an optional copy list and adds each address to `message.Cc`. Only the
withdrawal notice passes one — every other path sends to a single recipient, exactly as before.

### 1.10 Writing the outcome back

Three statements, in this order, after `Task.WhenAll`:

```csharp
		if (successList.Count > 0)
		{
			await _repository.UpdateBulkEmailInvitationRequestForSentEmailAsync(successList);
		}

		if (errorList.Count > 0)
		{
			await _repository.UpdateBulkEmailInvitationRequestForNotSentEmailAsync(errorList);
		}

		// Straight back to Pending with the attempt count untouched. No account would carry
		// them, so charging them an attempt would retire a valid address after five exhausted
		// passes without a single real delivery failure.
		if (abandonedList.Count > 0)
		{
			await _repository.ReleaseEmailInvitationClaimsAsync(abandonedList);
```

The two terminal writes, `ATSRepository.EmailInvitations.cs:213` and `:226`:

```csharp
			.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Done)
			.SetProperty(x => x.EmailSentAt, x => DateTime.UtcNow));
```

```csharp
			.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Error)
			.SetProperty(x => x.EmailClaimedAt, x => null)
			.SetProperty(x => x.EmailSendAttempts, x => x.EmailSendAttempts + 1));
```

`EmailSendAttempts` increments by **one per pass**, not per SMTP attempt. A pass that made three
transient attempts charges one. That is what makes the effective ceiling 15 rather than 5 (C3), and
it is why the design doc's `EmailSendAttempts = 5` diagnostic query means "five passes", not "five
sends". Neither write has a status guard — they match on id alone, which is safe only because
`RequeueEmailInvitationAsync` refuses to touch a `Processing` row (§6).

The deferral, `:176`:

```csharp
	public async Task<int> ReleaseEmailInvitationClaimsAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		// Deliberately does NOT touch EmailSendAttempts. These rows were claimed but never
		// offered to the SMTP server - the pass stood down because the provider was rate
		// limiting. Charging them an attempt would retire a perfectly valid address after
		// five throttles without a single real delivery failure.
		var ids = emailInvitationRequests.Select(x => x.EmailInvitationID).ToList();

		return await _dbcontext.EmailInvitationRequests
			.Where(x => ids.Contains(x.EmailInvitationID))
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Pending)
				.SetProperty(x => x.EmailClaimedAt, x => null));
	}
```

The comment's "never offered to the SMTP server" is *almost* true and the gap is worth knowing: a
row lands in `abandonedBag` whenever `SendWithRetryAsync` returns `Throttled`, which includes the
case where the first account accepted-then-421'd and every later account refused. The message was
offered. It was not accepted — a 421 is a refusal — so no duplicate arises, but the invariant is
"never accepted", not "never offered".

### 1.11 The notification that follows

```csharp
		// After the statuses are written, so the completeness check reads the outcome of
		// this pass rather than the state before it. Deferred rows are excluded on purpose:
		// they are still in flight, and a file is only finished when nothing remains.
		var attempted = successList
			.Concat(errorList)
			.Select(request => request.EmailInvitationID)
			.ToList();

		await _notificationService.RaiseForCompletedBulkEmailsAsync(attempted, cancellationToken);
```

`AtsNotificationService.RaiseForCompletedBulkEmailsAsync` (`:182`) resolves the bulk files those
ids belong to and qualifies a file only when nothing is left in flight —
`AtsNotificationRepository.GetCompletedBulkEmailFilesAsync` (`:104`):

```csharp
		// Counted across the whole file, not the batch: the job sends in claimed slices, so
		// a file is only finished when none of its orders are still Pending or Processing.
		var progress = await _dbContext.EmailInvitationRequests
			...
			.Select(group => new
			{
				FileId = group.Key,
				TotalCount = group.Count(),
				SentCount = group.Count(order => order.EmailSentStatus == EmailStatus.Done),
				FailedCount = group.Count(order => order.EmailSentStatus == EmailStatus.Error),
				InFlightCount = group.Count(order =>
					order.EmailSentStatus == EmailStatus.Pending
					|| order.EmailSentStatus == EmailStatus.Processing)
			})
			.Where(file => file.InFlightCount == 0)
```

The body the uploader sees:

```csharp
			var body = file.FailedCount == 0
				? $"All {file.SentCount} of {file.TotalCount} invitation emails for {fileLabel} have been sent."
				: $"{file.SentCount} of {file.TotalCount} invitation emails for {fileLabel} were sent. {file.FailedCount} could not be delivered and can be resent.";
```

Single (non-bulk) orders have `BulkFileID = null` and are excluded by the first query — they get
their own notification at creation time on the enrolment path.

Neither `RaiseAsync` nor `AtsNotificationRepository.AddAsync` dedupes:

```csharp
	public async Task AddAsync(AtsNotification notification, CancellationToken cancellationToken)
	{
		_dbContext.Notifications.Add(notification);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}
```

Combined with `attempted` being recomputed every pass, that is §10.6.

Also worth knowing: `AtsNotificationType.InvitationEmailFailed` exists, has a UI icon, a colour and
a `ShouldToast` case — and **nothing raises it**. It appears only in `BuildOrderMessage`'s message
map (`AtsNotificationService.cs:144`). A permanently rejected invitation produces no bell of its
own; the failure is only ever visible inside a bulk file's completion message, or in
`EmailSentStatus = Error` on the board.

---

## 2. The pacing mechanism — `SmtpRateLimiter` and `SmtpConnectionPool`

The registry, the context cache and the per-account ownership model are accounts §4.2. What follows
is the pacing itself, which the delivery doc owns.

### 2.1 The real configured numbers

**There is no `AtsEmailDelivery` section anywhere in the repository** (C11) — not in
`appsettings*.json`, not in `docker-compose*.yml`, not in any env file. `services.Configure<AtsEmailDeliveryOptions>(configuration.GetSection(AtsEmailDeliveryOptions.SectionName))`
(`ATSServiceConfiguration.cs:226`) therefore binds an empty section and every value comes from the
property initialisers in `Configuration/AtsEmailDeliveryOptions.cs`. These are the live numbers:

| Key | Value in force | The two incident-driven limits |
|---|---|---|
| `MaxConcurrentConnections` | `2` | |
| **`MaxSendsPerSecond`** | **`0.9`** | **Incident 1** — the send limit |
| **`MinSecondsBetweenLogins`** | **`5`** | **Incident 2** — the login limit |
| `MaxMessagesPerConnection` | `50` | |
| `SendTimeoutSeconds` | `60` | |
| `MaxAttemptsPerPass` | `3` | |
| `RetryBaseDelaySeconds` | `2` | |
| `ThrottleBackoffSeconds` | `600` | |
| `LoginThrottleBackoffSeconds` | `1_800` | |
| `ConsecutiveFailureThreshold` | `3` | |
| `TransientFailureCooldownSeconds` | `900` | |
| `DefaultDailySendLimit` | `450` | |
| `QuotaWindowHours` | `24` | |
| `SendLogRetentionHours` | `48` | |

The two the design doc §2 calls "two incidents, two different limits" are `MaxSendsPerSecond` and
`MinSecondsBetweenLogins`, and both numbers are derived in the source comments rather than chosen:

```csharp
	// Messages per second across ALL connections. The token bucket enforces this globally,
	// so raising MaxConcurrentConnections alone cannot outrun the provider.
	//
	// Gmail accepted 14 messages in roughly 8 seconds (~1.75/s) before refusing. Half that
	// is the sustainable rate, and it still clears 200 invitations in about three minutes.
	public double MaxSendsPerSecond { get; set; } = 0.9;
```

```csharp
	// Minimum gap between opening NEW authenticated sessions, paced separately from sends.
	//
	// Providers throttle authentication on its own budget - Gmail answers "454 Too many
	// login attempts" long before it complains about message volume. Pacing messages does
	// nothing for that, because a discarded session forces a fresh login that the send
	// limiter never sees. Five seconds means a pool of 2 refills in ten, and a pathological
	// reconnect loop still cannot exceed 12 logins a minute.
	public int MinSecondsBetweenLogins { get; set; } = 5;
```

So: 0.9/s is *half the observed 1.75/s ceiling*, and 5s is *the gap that caps a pathological
reconnect loop at 12 logins/minute*. Both are per account (C2) — the comment on
`MaxSendsPerSecond` still says "across ALL connections … globally", which was true when there was
one sender and is now true only within one mailbox.

`SendTimeoutSeconds = 60` carries its own incident:

```csharp
	// Generous on purpose. A 10s timeout was firing while the provider had ALREADY accepted
	// the message, so the send was recorded as failed and retried - which is how one
	// candidate received the same invitation several times.
	public int SendTimeoutSeconds { get; set; } = 60;
```

### 2.2 `SmtpRateLimiter` — two budgets, one gate

`Services/EmailService/SmtpRateLimiter.cs`. It is **not** a token bucket despite the class comment
using the phrase; it is a next-slot reservation with a single `SemaphoreSlim(1, 1)` serialising the
reservation. Both budgets share `_throttledUntilUtc` and both share `_gate`:

```csharp
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly ILogger<SmtpRateLimiter> _logger;
	private readonly double _minIntervalTicks;
	private readonly TimeSpan _minLoginInterval;

	private DateTime _nextSlotUtc = DateTime.MinValue;
	private DateTime _nextLoginSlotUtc = DateTime.MinValue;

	// Set when the provider says "slow down". Every waiter parks until it passes, which is
	// what stops a pass from spending its whole retry budget against a closed door.
	private DateTime _throttledUntilUtc = DateTime.MinValue;
```

The constructor, including the asymmetric guard that is C12:

```csharp
		var perSecond = options.Value.MaxSendsPerSecond;

		// A non-positive rate would mean "never send", which is never what was meant -
		// treat it as unconfigured and fall back to the documented default.
		if (perSecond <= 0)
		{
			perSecond = new AtsEmailDeliveryOptions().MaxSendsPerSecond;
		}

		_minIntervalTicks = TimeSpan.TicksPerSecond / perSecond;

		_minLoginInterval = TimeSpan.FromSeconds(
			Math.Max(0, options.Value.MinSecondsBetweenLogins));
```

`WaitForSlotAsync` (`:66`) — reserve under the lock, wait outside it:

```csharp
		// The reservation is taken under the lock, but the WAIT happens outside it.
		// Sleeping while holding the gate would serialise every sender behind one another
		// and collapse the concurrency the connection pool exists to provide.
		await _gate.WaitAsync(cancellationToken);

		try
		{
			var now = DateTime.UtcNow;

			// A live throttle outranks the normal cadence: the next slot moves to the end
			// of the back-off rather than one interval from now.
			var earliest = _throttledUntilUtc > now ? _throttledUntilUtc : now;

			if (_nextSlotUtc < earliest)
			{
				_nextSlotUtc = earliest;
			}

			var slot = _nextSlotUtc;

			_nextSlotUtc = slot.AddTicks((long)_minIntervalTicks);

			delay = slot - now;
		}
		finally
		{
			_gate.Release();
		}

		if (delay > TimeSpan.Zero)
		{
			await Task.Delay(delay, cancellationToken);
		}
```

`WaitForLoginSlotAsync` (`:115`) is the identical shape against `_nextLoginSlotUtc` and
`_minLoginInterval`, plus one log line — and its `<remarks>` is the incident-2 statement of record:

```csharp
	/// Paced separately from sends, and this is not a refinement - it is the fix for a real
	/// failure. Authentication has its own budget at the provider ("454 Too many login
	/// attempts" arrives long before any complaint about volume), and a discarded session
	/// forces a login that the send limiter never sees. Without this gate, one throttled
	/// send could produce an unbounded stream of logins, each one provoking the next
	/// throttle.
```

`ReportThrottled` (`:157`) is monotonic:

```csharp
			// Never shorten an existing throttle: two workers hitting the limit at once
			// must not let the second one's shorter window undo the first one's.
			if (until > _throttledUntilUtc)
			{
				_throttledUntilUtc = until;
			}
```

That rule is what preserves the 30-minute login back-off when `ReportFailureAsync` subsequently
calls `ReportThrottled` with 10 minutes (§10.3) — in memory. It is also why the two throttle
read-only properties are identical:

```csharp
	public bool IsLoginThrottled => DateTime.UtcNow < _throttledUntilUtc;
```
```csharp
	public bool IsThrottled => DateTime.UtcNow < _throttledUntilUtc;
```

One window, two names, two callers: `IsLoginThrottled` is read by the pool to refuse a new session
outright; `IsThrottled` exists for a processor that no longer reads it (the multi-account switcher
replaced that need — grepping `IsThrottled` finds the declaration, the two tests, and no
production caller in the delivery path).

### 2.3 `SmtpConnectionPool` — reuse first, login last

`Services/EmailService/SmtpConnectionPool.cs`. `AcquireAsync` (`:69`):

```csharp
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _available.WaitAsync(cancellationToken);

		try
		{
			// Reuse an idle session when one is healthy and has budget left; otherwise
			// build a fresh one. Retire-by-count is deliberate: providers cap how long a
			// single authenticated session may live, and a very old connection is also
			// more likely to have gone silently stale behind a NAT.
			while (_idle.TryTake(out var pooled))
			{
				if (pooled.IsUsable(_options.MaxMessagesPerConnection))
				{
					return new SmtpLease(this, pooled);
				}

				await pooled.DisposeAsync();
			}

			var connection = await CreateConnectionAsync(cancellationToken);

			return new SmtpLease(this, connection);
		}
		catch
		{
			// The slot must come back even when connecting failed, or a provider outage
			// permanently shrinks the pool.
			_available.Release();
			throw;
		}
```

`_available` is `new SemaphoreSlim(maxConnections, maxConnections)` built per pool, i.e. per
account, with the comment "a shared cap would leave the second account idling behind the first's
connections". The semaphore slot is released only in `ReturnAsync`'s `finally`, so it is held for
the whole lease.

`IsUsable` is the retirement rule:

```csharp
		public bool IsUsable(int maxMessages) =>
			Client.IsConnected
			&& Client.IsAuthenticated
			&& MessagesSent < maxMessages;
```

`CreateConnectionAsync` (`:104`) is where incident 2 is fixed, in this order:

```csharp
		// Refused outright rather than queued. Waiting out a 30-minute login back-off here
		// would hold a pool slot for the duration and starve the sessions that are still
		// perfectly usable.
		if (_rateLimiter.IsLoginThrottled)
		{
			throw new SmtpLoginThrottledException(
				"The SMTP provider is rate limiting authentication; no new session was opened.");
		}

		// Paced on its own budget. Authentication is throttled separately from volume at the
		// provider, and a discarded session forces a login the SEND limiter never sees -
		// which is how one bad response used to cascade into a stream of logins.
		await _rateLimiter.WaitForLoginSlotAsync(cancellationToken);
```

then the client, with the timeout that incident 1 made 6× larger:

```csharp
		var client = new MailKit.Net.Smtp.SmtpClient
		{
			// Applies to every network operation on this client. Generous on purpose: a
			// tight timeout fires while the provider has already accepted the message,
			// which records a false failure and triggers a duplicate send on retry.
			Timeout = (int)TimeSpan.FromSeconds(_options.SendTimeoutSeconds).TotalMilliseconds
		};
```

then connect + authenticate, and the classification that used to be missing:

```csharp
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			client.Dispose();

			// Classified HERE, not left to escape as an unclassified exception. "454 Too many
			// login attempts" is raised by AuthenticateAsync, so the send path's handler
			// never saw it: it was reported as a generic transient fault and retried, opening
			// yet another connection. The throttle has to be recognised at the point the
			// login happens.
			var classified = SmtpFailureClassifier.ClassifyConnectFailure(exception);

			if (classified.Outcome == EmailDeliveryOutcome.Throttled)
			{
				// The longer back-off: authentication limits are enforced over a wider
				// window than send limits, so the ten-minute send pause does not clear one.
				_rateLimiter.ReportThrottled(
					TimeSpan.FromSeconds(_options.LoginThrottleBackoffSeconds));
```

Line 157. This is the only place `LoginThrottleBackoffSeconds` is used in the delivery path, and it
writes to memory only (C1).

The counter the design doc §5 calls the leading indicator:

```csharp
		var totalLogins = Interlocked.Increment(ref _loginCount);

		_logger.LogInformation(
			"Opened SMTP session to {Host}:{Port} as {Sender}. Logins this process: {LoginCount}.",
			_credentials.SmtpHost,
			_credentials.SmtpPort,
			_credentials.EmailAddress,
			totalLogins);
```

with the field comment: "it should stay close to MaxConcurrentConnections over a whole run. If it
climbs with the message count, the pool is not pooling and the provider is about to say so." It is
per pool, so per account — "this process" in the message text means "this account in this process".

`SmtpLease` is the borrow/return handle, and `MarkFaulted` is the only way a session is condemned:

```csharp
	public void RecordSend() => _connection.RecordSend();

	public void MarkFaulted() => _faulted = true;

	public ValueTask DisposeAsync() => _pool.ReturnAsync(_connection, !_faulted);
```

### 2.4 What the three bounds actually add up to

At the defaults, one pass claims 200 rows, runs 4 in flight, and paces at 0.9 messages/second per
account — so 200 invitations take ≈222 s through one account, matching the design doc's "roughly
3.7 minutes". Two registered accounts halve that only if the switcher actually has to move messages
between them; on the happy path everything goes through the highest-priority sendable account and
the second sits idle, because `GetNextSendableAccountAsync` returns the *first* match and the
switcher only advances on a retryable account fault. **Registering a second account buys quota and
failover, not throughput** — consistent with accounts §11, and worth restating here because the
delivery doc §4 says "two accounts clear the same 200 in about half the time", which the selection
rule does not deliver on its own.

The retry budget: `MaxAttemptsPerPass = 3` SMTP attempts per pass, × `MaxEmailSendAttempts = 5`
passes, with `EmailSendAttempts` incremented once per pass. Back-off between in-pass attempts is
2 s, 4 s (the third attempt has no trailing delay). So a persistently transient address costs up to
15 SMTP attempts spread over five ticks — C3.

---

## 3. Failure classification — `SmtpFailureClassifier`

`Services/EmailService/SmtpFailureClassifier.cs`. One `public static class`, two entry points, two
private substring predicates. The class `<summary>` states why it is shared rather than living in
the send path:

```csharp
/// This is shared rather than living in the send path because the first version of this
/// code only classified send failures. "454 Too many login attempts" is raised by
/// AuthenticateAsync, so it escaped the send path's try/catch entirely, was reported as an
/// unclassified transient fault, and got retried - which opened another connection and
/// produced another 454. The classifier has to be reachable from the place the connection
/// is built, or the login throttle is invisible to the code that must react to it.
```

### 3.1 The two predicates — quoted in full, because they are the whole rule

```csharp
	private static bool LooksLikeThrottle(string? message) =>
		message is not null
		&& (message.Contains("try again later", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("unusual rate", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("too many", StringComparison.OrdinalIgnoreCase));
```

```csharp
	private static bool LooksLikeSenderRejection(string? message) =>
		message is not null
		&& (message.Contains("daily user sending limit", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("daily sending quota", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("sending limit exceeded", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("sender address rejected", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("not allowed to send", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("account has been disabled", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("account is disabled", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("account suspended", StringComparison.OrdinalIgnoreCase));
```

`LooksLikeSenderRejection`'s `<remarks>` forbids widening it, and names the consequence:

> Widening this to anything resembling "5xx looks serious" would put recipient failures
> back in the account bucket and reintroduce the exact bug the split exists to prevent.

`LooksLikeThrottle` has no equivalent warning, and its `"too many"` term is the loosest string in
the file — §10.5.

### 3.2 `ClassifySendFailure` (`:93`) — outcome, scope, and whether the session survives

```csharp
	public static (EmailDeliveryResult Result, bool SessionIsUsable) ClassifySendFailure(
		MailKit.Net.Smtp.SmtpCommandException exception)
	{
		var code = (int)exception.StatusCode;
		var codeText = code.ToString(CultureInfo.InvariantCulture);

		if (code is 421 or 454 || LooksLikeThrottle(exception.Message))
		{
			// 421 is "service closing transmission channel" - the server has hung up, so the
			// session is genuinely gone. Every other throttle leaves the connection open,
			// and keeping it is what avoids a re-login.
			var serverClosedConnection = code == 421;

			return (
				EmailDeliveryResult.Throttled(codeText, exception.Message),
				!serverClosedConnection);
		}

		// A per-recipient rejection says nothing about the connection: the session is still
		// good and the next message can go down it.
		//
		// Nor does it say anything about the ACCOUNT - with one exception. Most 5xx codes here
		// are about the address we just gave the server, but a few are the server refusing the
		// SENDER, and those two must not be conflated: counting a bad recipient against the
		// account would let one bulk upload of typo'd addresses retire every registered
		// mailbox, while ignoring a rejected sender would keep re-offering messages to an
		// account the provider has already disowned.
		var scope = code >= 500 && LooksLikeSenderRejection(exception.Message)
			? EmailFailureScope.Account
			: EmailFailureScope.Message;

		return code >= 500
			? (EmailDeliveryResult.Permanent(codeText, exception.Message, scope), true)
			: (EmailDeliveryResult.Transient(codeText, exception.Message, scope), true);
	}
```

Read as a decision table, with the account-switching consequence of each row:

| Server said | Outcome | Scope | Session | Switches account? | Retries same row? |
|---|---|---|---|---|---|
| `421` | `Throttled` | `Account` (by construction) | **discarded** | **Yes** | No — deferred, attempt not charged |
| `454` | `Throttled` | `Account` | kept | **Yes** | No — deferred |
| any code whose text matches `LooksLikeThrottle` | `Throttled` | `Account` | kept | **Yes** | No — deferred |
| 5xx matching `LooksLikeSenderRejection` | `Permanent` | **`Account`** | kept | **Yes** | No — `Error`, fails on attempt 1 |
| any other 5xx (e.g. `550 no such mailbox`) | `Permanent` | `Message` | kept | **No** | No — `Error`, fails on attempt 1 |
| any other 4xx (e.g. `451`) | `Transient` | `Message` | kept | **No** | Yes — back-off, up to 3 in-pass attempts |

The throttle test runs **before** the 5xx test. That ordering is what makes §10.5 possible.

### 3.3 `ClassifyConnectFailure` (`:33`) — no recipient has been named yet

```csharp
		if (exception is MailKit.Security.AuthenticationException authException)
		{
			// MailKit wraps the server's refusal. A throttle-shaped message is the provider
			// rate limiting logins; anything else is a genuinely bad credential, which no
			// amount of retrying will fix.
			//
			// Scoped to the ACCOUNT either way. Note the status code is null here - MailKit's
			// AuthenticationException does not carry one - which is exactly why the scope has
			// to be decided at this point: nothing downstream can recognise a 535 by looking.
			return LooksLikeThrottle(authException.Message)
				? EmailDeliveryResult.Throttled("454", authException.Message)
				: EmailDeliveryResult.Permanent(
					null,
					authException.Message,
					EmailFailureScope.Account);
		}

		if (exception is MailKit.Net.Smtp.SmtpCommandException commandException)
		{
			var code = (int)commandException.StatusCode;

			if (code is 421 or 454 || LooksLikeThrottle(commandException.Message))
			{
				return EmailDeliveryResult.Throttled(
					code.ToString(CultureInfo.InvariantCulture),
					commandException.Message);
			}

			// Everything raised during CONNECT or AUTHENTICATE belongs to the account: no
			// recipient has been named yet, so the server cannot be complaining about one.
			return code >= 500
				? EmailDeliveryResult.Permanent(
					code.ToString(CultureInfo.InvariantCulture),
					commandException.Message,
					EmailFailureScope.Account)
				: EmailDeliveryResult.Transient(
					code.ToString(CultureInfo.InvariantCulture),
					commandException.Message,
					EmailFailureScope.Account);
		}

		// A socket that will not open, a TLS negotiation that failed, a DNS miss. Transient:
		// the provider may simply be unreachable right now.
		//
		// Scoped to the account even though the cause may be the network. Moving to the next
		// account costs one retry; staying on a host we cannot reach costs the whole pass. If
		// the network is down, the next account fails the same way and the pass ends anyway.
		return EmailDeliveryResult.Transient(null, exception.Message, EmailFailureScope.Account);
```

**Every** connect/authenticate failure is `Account`-scoped, and a `Throttled` from here is stamped
with the literal `"454"` even when the server sent no code at all — the `AuthenticationException`
branch has nothing to read. That is why `EmailDeliveryResult.IsAccountFault` cannot be re-derived
downstream from `StatusCode` (see the `<remarks>` on that property, quoted in §3.4).

The `Throttled` factory makes the scope non-optional:

```csharp
	// Always the account's fault by definition: a throttle is the provider talking about this
	// sender's rate, never about who the message was addressed to.
	public static EmailDeliveryResult Throttled(string? code, string? message) =>
		new(EmailDeliveryOutcome.Throttled, code, message, EmailFailureScope.Account);
```

### 3.4 `EmailDeliveryResult` — the two flags, and why one is narrower

`Services/EmailService/EmailDeliveryResult.cs`. The record:

```csharp
public sealed record EmailDeliveryResult(
	EmailDeliveryOutcome Outcome,
	string? StatusCode = null,
	string? Message = null,
	EmailFailureScope Scope = EmailFailureScope.Message)
```

`IsAccountFault` (`:86`) drives the **breaker**; `CanRetryOnAnotherAccount` (`:104`) drives the
**switcher**. The difference between them is the double-send guard, and its `<remarks>` is the
canonical explanation:

```csharp
	public bool IsAccountFault =>
		!IsSent && Scope == EmailFailureScope.Account;
```

```csharp
	/// <remarks>
	/// Narrower than <see cref="IsAccountFault"/>, and deliberately so. The breaker counts every
	/// account fault, but only some of them are safe to resend immediately.
	///
	/// A throttle or an auth rejection is refused before the message body is ever accepted, so
	/// nothing was delivered and sending it elsewhere delivers it once. A transient socket drop
	/// or timeout is the dangerous case: it can fire AFTER the provider accepted the message -
	/// that is how a candidate received the same invitation twice - so resending it on another
	/// account in the same breath would turn a rare duplicate into a reliable one. A transient
	/// still counts toward the breaker and can still take the account out of rotation for
	/// SUBSEQUENT messages; this one is left to the normal deferral path.
	/// </remarks>
	public bool CanRetryOnAnotherAccount =>
		Scope == EmailFailureScope.Account
		&& Outcome is EmailDeliveryOutcome.Throttled or EmailDeliveryOutcome.Permanent;
```

**The mechanism, spelled out.** Both `SendOverContextAsync`'s transport catch and its protocol
catch return `Transient(..., EmailFailureScope.Account)`. So for those two:

- `IsAccountFault` → `true` → `ReportFailureAsync` counts it toward
  `ConsecutiveFailureThreshold` (3) and can cool the account down for
  `TransientFailureCooldownSeconds` (900). The account *may* leave rotation.
- `CanRetryOnAnotherAccount` → `false`, because `Outcome` is `Transient` and the predicate admits
  only `Throttled` or `Permanent`. The switcher's `if (result.IsSent || !result.CanRetryOnAnotherAccount) return result;`
  therefore returns immediately, with the *same* result object, and `attemptedAccountIds` never
  grows. No second SMTP conversation happens for this message in this call.

The message then goes back up to `SendWithRetryAsync`, whose `switch` has no `Transient` case — it
falls through to the back-off and retries **on the same account selection path** on attempt 2, or
returns `Transient` after `MaxAttemptsPerPass` and lands in `errorBag`. Either way the duplicate
risk is deferred to a later attempt with a delay in front of it, never taken inside the same breath.

Widening `CanRetryOnAnotherAccount` to include `Transient` would remove exactly that guard, which
is why the design doc §6 lists it under "what not to do". Note that narrowing it further would
break the *auth-rejection* case: a `Permanent`/`Account` from `ClassifyConnectFailure` (bad app
password) must switch, because retrying the same account produces the identical refusal.

`IsAccountFault`'s own `<remarks>` explains why the scope is carried rather than re-derived:

> Carried on the result rather than re-derived by the caller, because by the time a
> result reaches the processor the evidence is gone: MailKit's AuthenticationException
> has no status code, so `SmtpFailureClassifier.ClassifyConnectFailure` returns a Permanent
> with a NULL `StatusCode`. Anything downstream trying to recognise "535" by sniffing the
> code would silently never match, and a wrong password would look exactly like a bad
> recipient address.

The breaker side — `ReportFailureAsync`'s first line, `if (!result.IsAccountFault) return false;` —
is accounts §4.4 and is not repeated here.

---

## 4. State on the row — `EmailStatus` and the four columns

`Constants/EmailStatus.cs`, the whole file:

```csharp
namespace ATS.Constants;

internal static class EmailStatus
{
	internal const string Pending = "Pending";
	internal const string Processing = "Processing";
	internal const string Done = "Done";
	internal const string Error = "Error";
}
```

`internal`, no `All` array — the deliberate contrast with `TicketStatus`
(`public`, with `All`, because a caller-supplied filter has to be validated against it) is noted in
the ticketing companion §1.4. `EmailStatus` never crosses the API boundary as an input; it crosses
as an *output* string on list DTOs, which is why the UI keeps its own copy (§9).

The columns, `Data/Entities/EmailInvitationRequest.cs:18-30`:

```csharp
	public string? HashToken { get; set; }
```
```csharp
	public string? EmailSentStatus { get; set; }
	public DateTime? EmailSentAt { get; set; }
	public DateTime? EmailClaimedAt { get; set; }
	public int EmailSendAttempts { get; set; }
```
```csharp
	public DateTime? HashTokenCreatedAt { get; set; }
	public DateTime? HashTokenExpiration { get; set; }
```

Fluent configuration, `Data/EntityConfiguration/EmailInvitationRequestConfiguration.cs`:

```csharp
		builder.Property(e => e.EmailSentStatus)
			   .HasMaxLength(255)
			   .IsRequired(true);
```
```csharp
		builder.Property(e => e.EmailClaimedAt)
			   .IsRequired(false);

		builder.Property(e => e.EmailSendAttempts)
			   .IsRequired(true)
			   .HasDefaultValue(0);
```
```csharp
		// Drives the email notification job's claim query and the stale-claim sweeper.
		builder.HasIndex(e => e.EmailSentStatus);
```

`EmailSentStatus` is `IsRequired(true).HasMaxLength(255)` — the asymmetry the ticketing companion
§1.2 uses to explain why legacy orders have `TicketStatus = NULL` and are unqueued. The email
queue has the opposite problem: every row has a status, so nothing is silently unqueued, and the
`varchar(255)` is 5× wider than `TicketStatus`'s `varchar(50)` for four constants that are at most
ten characters.

Exactly one index, on `EmailSentStatus` alone. The claim query's `PARTITION BY "ClientId" ORDER BY
"OrderCreatedAt"` and its `EmailSendAttempts < 5` predicate are not covered by it, and the
sweeper's `(EmailSentStatus, EmailClaimedAt)` pair is not either — the write-hot-table reasoning in
the ticketing companion §1.2 applies unchanged.

Which column means what, in one place:

| Column | Set by | Read by |
|---|---|---|
| `EmailSentStatus` | claim query → `Processing`; `UpdateBulk…ForSentEmail` → `Done`; `UpdateBulk…ForNotSentEmail` → `Error`; `ReleaseEmailInvitationClaims` / `ReleaseStaleEmailInvitationClaims` / `RequeueEmailInvitation(s)` → `Pending` | claim query, sweeper, `GetCompletedBulkEmailFilesAsync`, every board |
| `EmailClaimedAt` | claim query (`DateTime.UtcNow`); nulled by all three release/error paths | the stale sweeper's cutoff only |
| `EmailSendAttempts` | `+1` in `UpdateBulkEmailInvitationRequestForNotSentEmailAsync`; `= 0` in both requeue methods | the claim query's `Error` re-claim gate |
| `EmailSentAt` | `UpdateBulk…ForSentEmail`, `UpdateSingle…ForSentEmail`; nulled by requeue | reporting only — nothing in the delivery path reads it |

Note that `EmailSentAt` is **not** set by the claim query and is not used as a dedupe key anywhere.
There is no idempotency token on the SMTP side: the only thing preventing a second delivery is the
status column being right (§10.1, §10.2).

---

## 5. The other send entry points, as diffs from §1

`ATSEmailService` implements both `IEmailService` (BuildingBlocks, shared with Auth) and
`IAtsEmailSender` (ATS-only). Four public send methods, one private worker:

| Method | Where | Caller | Diff from §1 |
|---|---|---|---|
| `SendATSEmailWithResultAsync` | `ATSEmailService.cs:61` | `EndorsementSubmissionService.SendApplicationFormToUserEmailWithResultAsync`; `WithdrawnEmailNotification.SendNoticeAsync` | The traced path. The switcher. The withdrawal notice is the only caller that passes `cc` |
| `SendATSEmailAsync` → `bool` | `:27` | `SendEmailAsync` (`:558`), `DisputeOrderService.cs:210` | Wraps the switcher and **discards the outcome**: `var result = await SendATSEmailWithResultAsync(toEmail, subject, body, CancellationToken.None); return result.IsSent;` — note `CancellationToken.None`, so this path cannot be cancelled at all |
| `SendThroughAccountAsync` | `:134` | **only** `SendATSEmailWithResultAsync:105` | One named account, no failover, reports to the breaker. Its interface doc claims registration uses it (C8); nothing outside the switcher does |
| `SendWithCredentialsAsync` | `:196` | `AtsEmailAccountManagementService.SendOtpThroughCredentialsAsync` (`:425`, calling it at `:431`) | Throwaway limiter + pool + context, all disposed at the end of the call. **No registry, no lease, no breaker, no quota check** |
| `SendOverContextAsync` (private) | `:240` | both of the above two real paths | The shared SMTP conversation, §1.9 |

`SendWithCredentialsAsync` in full, because its disposals are the interesting part:

```csharp
		// A limiter and pool of its own, disposed at the end of this call. The account has not
		// earned a place in rotation yet, so nothing here may be cached or reused - and a
		// verification send must not be paced behind a live account's queue.
		using var rateLimiter = new SmtpRateLimiter(
			Options.Create(_options),
			NullLogger<SmtpRateLimiter>.Instance);

		await using var pool = new SmtpConnectionPool(
			credentials,
			Options.Create(_options),
			rateLimiter,
			NullLogger<SmtpConnectionPool>.Instance);

		await using var context = new SmtpAccountContext(
			credentials.AtsEmailAccountId,
			credentials.DisplayName,
			credentials.EmailAddress,
			pool,
			rateLimiter);

		return await SendOverContextAsync(
			context,
			toEmail,
			subject,
			body,
			cancellationToken);
```

It reuses the same `AtsEmailDeliveryOptions` instance the injected service got, so a credentials
send is paced at 0.9/s and logs in at most once per 5 s — **but against a limiter nobody else can
see**. Accounts §1.4 covers the registration flow; the delivery-side consequence is §10.8.

### 5.1 Credentials: where the password is unprotected, and for how long

The task this document was asked to answer is "how the encrypted password is unprotected per
send". **It is not.** There are two distinct unprotect sites and neither is per send:

**(a) Once per context build, then held in a singleton.**
`SmtpAccountPoolRegistry.BuildContextAsync` (accounts §4.2) calls
`_secretProtector.Unprotect(account.EncryptedPassword, AtsEmailAccountSecrets.PasswordContext(account.EmailAddress))`
and passes the plaintext into `new SmtpAccountCredentials(...)`, which the pool stores:

```csharp
public sealed record SmtpAccountCredentials(
	int AtsEmailAccountId,
	string DisplayName,
	string EmailAddress,
	string AppPassword,
	string SmtpHost,
	int SmtpPort);
```

with the record's own warning:

> <paramref name="AppPassword"/> is the plaintext, already unprotected. It lives only in
> memory and must never be logged or put in a DTO.

So the plaintext lives for the **lifetime of the cached `SmtpAccountContext`** inside the singleton
registry — every send through that account reuses it, and it is released only by
`InvalidateAsync` (delete, edit, or a `Permanent` account-scoped failure). Accounts §7 covers the
protection scheme and its two closed leak paths; the delivery-side fact to remember is that a
per-send unprotect would cost a crypto operation per message and would not change the exposure
window materially, because the pool has to hold the plaintext to authenticate a replacement session
anyway.

**(b) Once per OTP send, on the management path.**
`AtsEmailAccountManagementService.UnprotectStoredPassword` (`:550`) — used by `DeleteAsync`
(`:259-263`) and `ResendOtpAsync` (`:366`) — unprotects the *stored* password of an account that may
already be in rotation, and hands it to `SendWithCredentialsAsync`:

```csharp
	private string UnprotectStoredPassword(AtsEmailAccount entity)
	{
		try
		{
			return _secretProtector.Unprotect(
				entity.EncryptedPassword,
				AtsEmailAccountSecrets.PasswordContext(entity.EmailAddress));
		}
		catch (System.Security.Cryptography.CryptographicException)
		{
			// Almost always a rotated Security:SecretProtectionKey. Saying so is the difference
			// between re-entering one password and hunting a decryption error.
			throw new BadRequestException(
				"The stored password for this account can no longer be read.",
				"This usually means the secret protection key was rotated. Re-enter the app password to continue.");
		}
	}
```

Note the two different exception shapes for the same failure: `BadRequestException` (400) on the
management path, `InvalidOperationException` — caught by `SendThroughAccountAsync` and turned into
a `Permanent`/`Account` result — on the registry path. Both are deliberate; a send must not 400, and
a management action must not silently defer.

### 5.2 The keyed `IEmailService` registrations — who registers what

```csharp
	public interface IEmailService
```
`BuildingBlocks/SharedServices/Interfaces/IEmailService.cs`, two members:

```csharp
	Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true);
```
```csharp
	Task<bool> SendATSEmailAsync(string toEmail, string subject, string body);
```

Neither returns an outcome, which is exactly why `IAtsEmailSender` exists as a separate ATS-only
interface rather than widening this one — its `<summary>`:

> <c>IEmailService</c> lives in BuildingBlocks and is implemented by Auth and the test
> fakes as well; widening it would force every implementer to reason about SMTP status
> codes they do not have.

| Key | Implementation | Registered at | Consumers |
|---|---|---|---|
| `"ats"` | `ATS.Services.EmailService.ATSEmailService` | `ATSServiceConfiguration.cs:148` | `EndorsementSubmissionService.cs:36`, `DisputeOrderService.cs:19` |
| `"ats"` (**again**) | the same `ATSEmailService` | `EmploymentVerificationServiceConfiguration.cs:57` | `EmploymentVerificationService.cs:16` |
| `"auth"` | `Auth`'s own `EmailService` | `AuthServiceConfiguration.cs:37` | `UserService`, `RegisterService`, `ForgotPasswordService`, `AppSubRoleService` |
| unkeyed `IAtsEmailSender` | forwarded from the keyed `"ats"` instance | `ATSServiceConfiguration.cs:155-156` | `AtsEmailAccountManagementService` (OTP sends) |

The forwarding registration and its reasoning are quoted in accounts §5.1 and are not repeated.
The **duplicate `"ats"` registration** is not mentioned anywhere in the existing docs — see §11.1.

`ATSEmailService` implements the three `IEmailService` body-builders it does not use as
`throw new NotImplementedException();` (`SendApprovalNotificationBody`, `SendNotificationBody`,
`SendOtpBody`, `SendPasswordResetBody`) — those belong to Auth's implementation. Resolving the
keyed `"ats"` service and calling one of them throws at runtime, not at compile time.

---

## 6. Operator-forced resend and requeue

The design doc §8 explains the three bugs the old inline resend caused and why
`RequeueEmailInvitationAsync` is the fix. `docs/features/ats-bulk-requeue/ats-bulk-requeue.md`
documents the bulk shape end to end (batch cap, per-row scope enforcement, the selection bar, the
partly-stale selection rule). What follows is only the delivery-path diff and the two slices'
relationship to the automatic path.

### 6.1 `ResendApplicationForm` — single row

```
PATCH ats/resendapplicationform
  → ResendApplicationFormEndpoint.AddRoutes            (Features/Web/ResendApplicationForm/)
    → sender.Send(ResendApplicationFormCommand)
      → ResendApplicationFormCommandValidator           (NotEmpty on the id, nothing else)
      → ResendApplicationFormCommandHandler.Handle
        → IEndorsementSubmissionService.ResendApplicationFormAsync(id, ct)
          → IATSRepository.GetEmailInvitationRequestByIdAsync      (404 if Guid.Empty)
          → IsInvitationWithinCallerScopeAsync                     (404, not 403, if out of scope)
          → ISecureToken.GenerateSecureToken → IHashService.Hash
          → IATSRepository.RequeueEmailInvitationAsync             ← the one statement
          → [0 rows? ConflictException "no longer awaiting a resend"]
          → IOrderHistoryService.RecordAsync(ApplicationFormResent) ← a SECOND write, no transaction
```

The endpoint returns the bool, not the response record it just built:

```csharp
			var response = new ResendApplicationFormResponse(result.Success);

			return Results.Ok(response.Success);
```

which is why the UI service deserialises a bare `bool` (§9). Its `WithDescription` is also stale in
a small way — it says the slice resets "the ticket status to 'Pending Candidate Info'", which is
`OrderStatus`, not `TicketStatus`; the requeue touches neither ticketing column.

`RequeueEmailInvitationAsync` (`ATSRepository.EmailInvitations.cs:64`) is quoted in full in the
design doc §8's table; the two comments that carry the correctness:

```csharp
		// Mirrors RequeueExhaustedTicketAsync. The predicate is the concurrency guard, not
		// just a lookup: matching on the current status inside the UPDATE means a row the
		// job has already claimed updates nothing, and the caller is told so. A
		// read-then-write would race and could resurrect a live claim.
		//
		// Processing is the one status excluded. That row is mid-send RIGHT NOW - a worker
		// is holding it in memory and is about to write its outcome, so re-issuing the token
		// here would both race that write and risk a second delivery.
		//
		// Pending is allowed even though the row is already queued: nothing has been sent, so
		// re-issuing the token duplicates no email, and refusing would give an operator a
		// confusing error for clicking resend twice.
```

```csharp
				// The budget resets: whatever blocked delivery is expected to have been
				// fixed, so the job gets a full set of automatic attempts again. Without
				// this a retried row was still at the ceiling and the claim query skipped
				// it, so the retry silently did nothing.
				.SetProperty(x => x.EmailSendAttempts, x => 0)
```

**How this differs from the automatic path, precisely:** the automatic path never resets
`EmailSendAttempts` (it only increments it, or leaves it alone for a deferral), never reissues
`HashToken`, and never touches `OrderStatus`/`ApplicationFormStatus`. The requeue does all four, and
it is the only writer in the repository that sets `EmailSendAttempts` back to zero. That is why a
resent row gets a *fresh* 5-pass budget rather than continuing the old one — 15 more SMTP attempts
available to an address that had already exhausted 15.

The excluded status is `Processing` only. `Done` is requeueable, by design: the UI's own copy calls
it "a nudge" (§9). So an operator can legitimately cause a second delivery to a candidate who
already received one — that is the feature, not a bug, and it is the reason `CanResend` in the UI
exists at all.

### 6.2 `ResendApplicationForms` — the set form, as a diff

Same shape, four differences:

| | Single | Bulk |
|---|---|---|
| Route | `PATCH ats/resendapplicationform` | `PATCH ats/resendapplicationforms` |
| Request record | `ResendApplicationFormRequest(Guid emailInvitationId)` — **camelCase** property | `ResendApplicationFormsEndpointRequest(IReadOnlyCollection<Guid> EmailInvitationIds)` — **PascalCase** |
| Scope check | one row, `IsInvitationWithinCallerScopeAsync`, out-of-scope → `NotFoundException` | `GetEmailInvitationOwnersAsync` then `IsOwnerWithinScope` **per row**, out-of-scope ids dropped silently; nothing in scope → `NotFoundException`; no ATS access → `ForbiddenException` |
| Cap | none | `MaxBulkResendSize = 500`, enforced in **both** the validator and the service |
| Response | `Results.Ok(response.Success)` — a bare `bool` | `Results.Ok(response)` — `RequestedCount` / `RequeuedCount` / `IsComplete` |
| Persistence | `RequeueEmailInvitationAsync` (one `ExecuteUpdateAsync`) | `RequeueEmailInvitationsAsync` — **one `ExecuteUpdateAsync` per row** in a loop |

The cap constant and its reasoning:

```csharp
	// A bulk resend is bounded because every requeued invitation becomes a message on the
	// deliberately-paced email queue. At the default 0.9 sends/second, 500 invitations is
	// already about nine minutes of sending; releasing thousands at once would block every
	// other client behind one operator's click.
	public const int MaxBulkResendSize = 500;
```

That is the delivery-side justification and it is the number to re-derive if `MaxSendsPerSecond`
changes: 500 ÷ 0.9 ≈ 556 s ≈ 9.3 min, and the claim batch is 200, so a 500-row requeue takes at
least three ticks to drain even with an empty queue.

The per-row loop, and the reason it is not one statement:

```csharp
		// One UPDATE per row rather than one for the set, because each invitation needs its
		// OWN freshly generated token - a shared token would let any candidate in the batch
		// open another candidate's form. They run inside the caller's transaction, so the
		// batch still commits or rolls back as a unit.
		//
		// The predicate matches RequeueEmailInvitationAsync: a row the job is actively
		// sending (Processing) is skipped rather than raced, and the returned count reflects
		// what actually moved.
```

"They run inside the caller's transaction" — **they do not.** `ResendApplicationFormsAsync` contains
no `TransactionRunner.RunAsync` and no `_unitOfWork` use; the two `TransactionRunner` calls in
`EndorsementSubmissionService` are at `:170` (single enrolment) and `:269` (bulk subject insert).
Each `ExecuteUpdateAsync` commits on its own, so a failure partway through a 500-row batch leaves
the earlier rows requeued with fresh tokens and no way to roll them back. Practically survivable —
a requeued row is a legitimate state — but the comment asserts a guarantee the code does not provide.

### 6.3 The pre-fix repository method is still on the interface

`ATSRepository.EmailInvitations.cs:296`:

```csharp
	public async Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken)
	{
		await _dbcontext.EmailInvitationRequests
			.Where(eir => eir.EmailInvitationID == emailInvitationId)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(eir => eir.HashToken, hashToken)
				.SetProperty(eir => eir.HashTokenCreatedAt, DateTime.UtcNow)
				.SetProperty(eir => eir.HashTokenExpiration, hashTokenExpiration)
				.SetProperty(eir => eir.OrderStatus, OrderStatus.PendingCandidateInfo)
				.SetProperty(eir => eir.ApplicationFormStatus, ApplicationFormStatus.Pending),
				cancellationToken);

		return true;
	}
```

This is exactly the write the design doc §8 describes as broken — token fields only, no
`EmailSentStatus`, no `EmailSendAttempts` reset, no status predicate (so it *will* overwrite a
`Processing` row's token mid-send). It is declared on `IEmailInvitationRepository.cs:52` and
forwarded by the cache decorator at `ATSCacheRepository.EmailInvitations.Cache.cs:88-95`, which even
invalidates `CacheTags.WithdrawnApplication` for it. **No production code calls it** — the service
calls `RequeueEmailInvitationAsync` instead. §10.9.

---

## 7. This job and the OMS ticketing job, side by side

`OMSTicketingBackgroundJob` was modelled on this one and the two now differ in ways that matter
when a change is ported between them. Same table, both columns verified against the code.

| | Email invitations | OMS ticketing |
|---|---|---|
| Job class | `EmailNotificationBackgroundJob` | `OMSTicketingBackgroundJob` |
| Tick | **5 s** (`EmailNotificationBackgroundJobSetup.cs:22`) | **10 s** (`OMSTicketingBackgroundJobSetup.cs:13`) |
| Concurrency | `[DisallowConcurrentExecution]` + `SKIP LOCKED` | identical |
| Claim method | `ATSRepository.GetPendingEmailInvitationRequestsAsync` (`:21`) | `OMSTicketingRepository.ClaimPendingTicketsAsync` |
| `FOR UPDATE SKIP LOCKED` | yes, inner sub-SELECT, `RETURNING t.*` | yes, same shape |
| Fair share | `ROW_NUMBER() OVER (PARTITION BY "ClientId" ORDER BY "OrderCreatedAt")`, `rn <= PerClientSliceSize` = **50** | same, `rn <= 30` |
| Batch cap | **bare literal `200`** at `:59`, no constant | `ClaimBatchSize = 50`, named `private const` |
| Outer terminal gate | none — status alone | extra `IsTicketed = false` |
| Re-claim of failures | `EmailSentStatus = Error AND EmailSendAttempts < 5` | `TicketStatus = Error AND TicketAttempts < 5` |
| Budget constant | `MaxEmailSendAttempts = 5`, `private`, repository-owned | `MaxTicketAttempts = 5`, **`public`** because the UI prints `5/5` |
| Attempts charged | **one per pass**, even though a pass makes up to 3 SMTP attempts | one per order outcome |
| Stale timeout | 30 min, processor-owned (`EmailNotificationProcessorService.cs:23`) | 30 min, processor-owned (`OMSTicketingProcessorService.cs:7`) |
| Sweeper charges an attempt | no | no |
| Fan-out | `SemaphoreSlim(MaxConcurrentConnections * 2)` = 4, **derived from config** (`:95`) | `SemaphoreSlim(MaxDegreeOfParallelism)` = 3, **`private const`** (`:11`) |
| DI scope granularity | **per send attempt**, inside `TrySendEmailAsync` | **per order**, inside `ProcessOneAsync` |
| Payload load | per attempt, inside the scope (`ResolveClientNameAsync`) | one batched `GetTicketPayloadsAsync(claimedIds)` before fan-out, plus an `Except` to park claimed-but-missing rows |
| Abort-the-pass signal | `throttleSignal` CTS linked into every send | none |
| Post-batch cache revocation | none in the processor | revokes `Report`, `DisputeOrder`, `WithdrawnApplication` |
| Notification raised | `RaiseForCompletedBulkEmailsAsync` (per pass) + `EmailAccountsExhausted` (per pass) | `NotifyIfTicketingExhaustedAsync` (per exhausted order) |
| Manual requeue | `RequeueEmailInvitationAsync` / `RequeueEmailInvitationsAsync`, excludes `Processing` | `RequeueExhaustedTicketAsync` / `RequeueExhaustedTicketsAsync` |
| Repository cached? | forwards through `ATSCacheRepository` **without** caching (`:15-27`) | registered directly, bypassing the decorator |

Three of these differences are the ones most likely to be "harmonised" wrongly:

**The fan-out is derived here and constant there.** Raising `MaxConcurrentConnections` widens the
email in-flight window automatically; the ticketing job needs a code edit. Neither changes
throughput — the email side is rate-bound, the ticketing side is bound by three stored-procedure
round trips to a remote SQL Server.

**The scope is per attempt here and per order there.** Both satisfy "never share a DbContext across
concurrent tasks". The email job's finer granularity means a three-attempt row creates three scopes
and re-resolves `IEndorsementSubmissionService` each time; the ticketing job resolves
`IOMSTicketingRepository`, `IAuthQueries` and `IOMSTicketCreator` once per order.

**The email job has an abort signal and the ticketing job does not.** That is not an omission on the
ticketing side — it has no shared, exhaustible resource to stand down for. It is also the source of
§10.1, which has no ticketing analogue.

The two claim queries are otherwise the same statement with different column names, and the
placeholder-binding trap is identical: `{0}`/`{1}` are the *written* values and appear first in the
argument list but last in the SQL.

---

## 8. The send log and its retention

`Data/Entities/AtsEmailSendLog.cs` — one row per **successful** send, and the entity's `<remarks>`
gives both design reasons:

```csharp
/// A counter column on <see cref="AtsEmailAccount"/> would be smaller, but it cannot express a
/// rolling window: it would need resetting at some fixed hour, and Gmail does not enforce its
/// limit at a fixed hour - a send at 23:00 still counts against you at 22:00 the next day. A
/// row per send makes the figure a COUNT over a WHERE, which is the same number the selector
/// and the UI read, so the table can never disagree with the routing decision.
///
/// Only SUCCESSFUL sends are logged. A message the provider refused did not consume quota, and
/// logging it would make the account look more consumed than it is and retire it early.
```

(The "COUNT over a WHERE" phrasing is stale: the actual read is a `SUM(RecipientCount)` — see
`AtsEmailAccountRepository.GetConsumedInWindowAsync` and the configuration comment "The only read
shape: SUM(RecipientCount) for one account over the last 24 hours". Accounts §4.5 has the query.)

```csharp
	// Recipients, not messages: Google counts the former. One invitation is one recipient
	// today, but a future CC or BCC would consume more quota than rows, and the selector must
	// count what the provider counts.
	public int RecipientCount { get; set; }
```

The CC that comment anticipated has arrived: the withdrawal notice copies the candidate and
`ccteam@cibi.com.ph`, so `SendThroughAccountAsync` now passes `1 + copied.Count` rather than the
literal `1`. The schema did not have to change — which is exactly what that comment predicted. See
`docs/features/ats-withdrawn-application-email/`.

Configuration, `Data/EntityConfiguration/AtsEmailSendLogConfiguration.cs`: table `ats."EmailSendLog"`,
`ValueGeneratedOnAdd` identity, cascade on the account FK, and one index:

```csharp
		// The only read shape: SUM(RecipientCount) for one account over the last 24 hours,
		// asked once per message. Covers the retention sweep's range delete as well.
		builder.HasIndex(x => new { x.AtsEmailAccountId, x.SentAt });
```

"Asked once per message" is the cost §10.10 refers to.

### 8.1 `AtsEmailSendLogRetentionService` — and how it differs from its two siblings

`BackgroundJobs/Notifications/AtsEmailSendLogRetentionService.cs`. A `BackgroundService`, not a
Quartz job — it does not need the clustered store or the scheduler, and running it outside Quartz
means it sweeps on **every** node rather than once per cluster. That is correct here (a range delete
is idempotent) and is the same choice `AtsNotificationRetentionService` and `AtsAuditRetentionService`
make.

```csharp
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Hourly. The rows are small and the cutoff moves continuously, so a long interval
		// would only ever mean a bigger delete doing identical work.
		using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
```

The window is the interesting line — it is not simply `SendLogRetentionHours`:

```csharp
	private async Task SweepAsync(CancellationToken cancellationToken)
	{
		var retentionHours = Math.Max(
			_options.QuotaWindowHours + 1,
			_options.SendLogRetentionHours);

		var cutoff = DateTime.UtcNow.AddHours(-retentionHours);

		using var scope = _scopeFactory.CreateScope();

		var repository = scope.ServiceProvider.GetRequiredService<IAtsEmailAccountRepository>();

		var deleted = await repository.DeleteSendLogsOlderThanAsync(cutoff, cancellationToken);
```

`Math.Max(QuotaWindowHours + 1, SendLogRetentionHours)` = `Math.Max(25, 48)` = 48 today. The `Max`
is the guard: if an operator lowered `SendLogRetentionHours` below the quota window, the sweep
would delete rows the `SUM` still needs and **understate consumption**, which reads as free capacity
and walks into the provider's cap. The class `<remarks>` says exactly this:

> The retention window is deliberately wider than the quota window: trimming at exactly 24
> hours would race the counting query and could subtract consumption an account has genuinely
> used, which reads as free capacity and walks straight into the provider's cap.

The delete itself, `AtsEmailAccountRepository.cs:245`:

```csharp
	public Task<int> DeleteSendLogsOlderThanAsync(
		DateTime cutoffUtc,
		CancellationToken cancellationToken) =>
		_dbContext.EmailSendLog
			.Where(log => log.SentAt < cutoffUtc)
			.ExecuteDeleteAsync(cancellationToken);
```

Compared with the two retention services it says it is modelled on, three things are missing:

| | `AtsEmailSendLogRetentionService` | `AtsNotificationRetentionService` | `PlatformLogRetentionService` |
|---|---|---|---|
| Enable flag | **none — always on** | `_options.RetentionEnabled` | `PostgreSqlEnabled && RetentionEnabled` |
| Interval | hardcoded `TimeSpan.FromHours(1)` | `RetentionIntervalHours`, floored at 1 | `RetentionIntervalHours`, floored at 1 |
| Batching | **one unbounded `ExecuteDeleteAsync`** | `Take(batchSize)` loop until a pass comes back short | `DeleteExpiredBatchAsync` loop until short |
| Exception handling | swallowed + logged | swallowed + logged | swallowed + logged |

The swallow-and-log is identical in all three and is the right call — the comment here is the
clearest statement of it:

```csharp
			catch (Exception exception)
			{
				// Swallowed on purpose: a failed sweep costs disk, while an escaping exception
				// from a BackgroundService takes the host down and stops every email with it.
				_logger.LogError(exception, "ATS email send log retention failed");
			}
```

The missing batching is §10.11.

---

## 9. Frontend — the resend round trip from the browser

Two components, three entry points, one UI service.

### 9.1 `Component/ATS/Withdrawn/WithdrawnApplicationComponent.razor.cs` — single row only

```
ConfirmResendApplicationForm(id)   (:19)
  → DialogService.ShowAsync<YesNoDialogComponent>(ConfirmActionAsync = () => ResendApplicationForm(id))
    → ResendApplicationForm(id)    (:95)
      → _loadingEmailInvitationId = id; await InvokeAsync(StateHasChanged)     ← the double-click guard
      → EndorsementSubmissionService.ResendApplicationFormAsync(id)
        → _httpClient.PatchAsJsonAsync("ats/resendapplicationform", new { emailInvitationId })
        → response.Content.ReadFromJsonAsync<bool>()
      → !IsSuccess → Snackbar(ErrorDetail) → false
      → !Data      → Snackbar("Failed to resend application form.") → false
      → lockedUsersTable.TableRef.ReloadServerData()   (its own try/catch: "resent, but the list could not be refreshed")
      → Snackbar("Application form resent successfully.") → true
```

The `_loadingEmailInvitationId` field is set before the await and cleared in `finally`, and the
button binds to it — the same double-click pattern the ticketing companion §6.4 describes. Note the
snackbar copy still says "resent", not "queued": theWithdrawn board does not tell the operator the
message goes out on the next tick, while the bulk dialog does.

### 9.2 `Component/ATS/BulkUploads/BulkUploadSubjectsDialog.razor.cs` — single **and** bulk

`ResendAsync(id)` (`:294`) is the single-row path, same service call, but its copy is queue-shaped:

```csharp
			// The row's statuses have just changed, so reload rather than patch in place.
			// The row now reads Pending with a cleared attempt count, which is the visible
			// confirmation that the retry took effect.
			await ReloadTableAsync();

			Snackbar.Add(
				"Application form queued for resending. It is normally delivered within a minute.",
				Severity.Success);
```

`ConfirmBulkResendAsync` (`:361`) → `YesNoDialogComponent` → `BulkResendAsync` (`:435`). The
dialog's warning text is the operator-facing statement of the pacing:

```csharp
				"Each subject gets a fresh application link, and their previous one stops "
					+ "working. They are sent at a steady rate, so a large batch can take "
					+ "several minutes to go out."
```

and the shortfall handling:

```csharp
			// A shortfall is normal rather than an error: the email job may have picked up
			// some of the selection between rendering and clicking. Saying so is more use
			// than a flat "done".
			if (result.IsComplete)
			{
				Snackbar.Add(
					$"{result.RequeuedCount} application form(s) queued for resending.",
					Severity.Success);
			}
			else
			{
				Snackbar.Add(
					$"{result.RequeuedCount} of {result.RequestedCount} application form(s) queued. "
						+ "The rest were already being sent.",
					Severity.Info);
			}
```

Selection is copied before the call (`var requestedIds = _selectedInvitationIds.ToList();`) because
the reload prunes the set, and is per page by design —
`ats-bulk-requeue.md` §4 has the reasoning.

### 9.3 `CanResend` — the UI-side double-send guard

```csharp
	// Resend is offered only where it helps. A Pending or Processing invitation is
	// already in the email job's queue, so resending would double-send; a completed
	// form has nothing left to fill in.
	private static bool CanResend(BulkUploadSubjectListDTO subject)
	{
		if (string.Equals(
			subject.ApplicationFormStatus,
			SubjectApplicationFormStatus.Done,
			StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (string.Equals(
			subject.EmailSentStatus,
			SubjectEmailSentStatus.Error,
			StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		// Delivered but not acted on, or withdrawn: a nudge is legitimate.
		return string.Equals(
			subject.EmailSentStatus,
			SubjectEmailSentStatus.Done,
			StringComparison.OrdinalIgnoreCase);
	}
```

and the blocked-reason string, which is the plainest statement of the hazard anywhere in the
feature:

```csharp
			? "This subject already completed their application form."
			: "The invitation email has not been sent yet. Resending would send it twice.";
```

**This guard exists only in the browser.** The backend requeue excludes `Processing` but allows
`Pending` and `Done` (by design, §6.1), so a direct `PATCH /ats/resendapplicationform` against a
`Pending` row requeues it — harmless, nothing was sent — and against a `Done` row causes a genuine
second delivery. The UI is the only thing that distinguishes those two, and it does so with a
client-side `static bool`.

### 9.4 The status vocabulary is hand-copied into the UI

`UI/FrontendWebassembly/DTO/ATS/BulkUploadDashboardDTO.cs:156`:

```csharp
// The values actually stored in EmailInvitationRequest.EmailSentStatus, used to render
// a single row's badge.
public static class SubjectEmailSentStatus
{
	public const string Pending = "Pending";

	public const string Processing = "Processing";

	public const string Done = "Done";

	public const string Error = "Error";
}
```

Four constants duplicated across an assembly boundary from an `internal` class the UI cannot
reference — the same arrangement the ticketing companion §6.2 flags for `OrderTicketStatus`.
Alongside it, `BulkSubjectEmailStatus` (`Pending` / `Sent` / `Failed`) is the *filter* vocabulary,
which is **not** the stored vocabulary: a stored `Processing` is reported to the user as `Pending`.
`BulkUploadSubjectsDialog.razor.cs:611-621` holds the mapping. Change a constant in
`ATS.Constants.EmailStatus` and three places need editing, with nothing enforcing it.

### 9.5 UI service — `Services/ATS/EndorsementSubmission/EndorsementSubmissionService.cs`

Same name as the backend service and a different thing entirely (an `HttpClient` wrapper), exactly
as the ticketing companion §2.1 warns. Both methods use the `"API"` client and the same
`try/catch` shape as the accounts UI service (§3.3 there): non-success → `ServiceResponse.Failure`
via `response.ReadErrorDetailAsync`; `OperationCanceledException` rethrown, not swallowed; network
and serialisation exceptions → a generic failure.

The asymmetry to remember is the response type: the single resend reads a **bare `bool`** (because
the endpoint returns `response.Success`), the bulk resend reads a **`BulkRetryResultDTO`**. Getting
either wrong is a silent deserialisation failure, not a compile error.

---

## 10. Sharp edges

Reported, not fixed. Ordered by how badly they can bite.

### 10.1 An aborted pass re-sends mail the provider already accepted

**Double-send.** `EmailNotificationProcessorService.cs:264`:

```csharp
				await Task.Delay(backoff, cancellationToken);
```

and `:123`:

```csharp
			await semaphore.WaitAsync(cancellationToken);
```

Neither is inside a `try/catch`. The token at `:264` is `linkedTokenSource.Token`, so when
`throttleSignal.CancelAsync()` fires at `:251` — from a *different* row's task, because that row
found every account exhausted — any task sitting in its retry back-off throws `TaskCanceledException`.
It escapes `SendWithRetryAsync`, escapes the lambda, faults `await Task.WhenAll(sendTasks)` at
`:154`, and **every write below it is skipped**, including
`UpdateBulkEmailInvitationRequestForSentEmailAsync(successList)`.

Rows whose messages the provider already accepted stay at `EmailSentStatus = Processing`. Thirty
minutes later `ReleaseStaleEmailInvitationClaimsAsync` flips them to `Pending` with no attempt
charged, the next pass claims them, and those candidates receive a second invitation. The
`TrySendEmailAsync` cancellation catch does not help: it guards the *send*, not the delay between
sends.

Graceful shutdown reaches the same place by the other door — `context.CancellationToken` is
signalled, `AddQuartzHostedService(options => options.WaitForJobsToComplete = true)`
(`ATSServiceConfiguration.cs:278`) makes Quartz wait, and both unguarded awaits throw. The design
doc's §6 rule "a timeout that fires after the provider accepted the message … is how the same
candidate gets emailed twice" describes a hazard that was closed at the SMTP layer and is still
open at the orchestration layer.

The shape of a fix is either to catch `OperationCanceledException` around the delay and return
`Throttled`, or to write the three status batches in a `finally`. Both are out of scope here.

### 10.2 A failed bookkeeping write turns a delivered message into a retry

**Double-send.** `ATSEmailService.cs:184` awaits `ReportSuccessAsync` *after* the send returned
`Sent`, outside any `try`:

```csharp
		if (result.IsSent)
		{
			// Recipients, not messages: the TO address plus everyone copied. The provider counts
			// recipients against the daily cap, so a copied message has to consume more of it -
			// see AtsEmailSendLog.RecipientCount, which is summed rather than counted.
			await _poolRegistry.ReportSuccessAsync(accountId, 1 + copied.Count, cancellationToken);
		}
```

`ReportSuccessAsync` opens a scope and performs two database writes (accounts §4.4). If either
throws — connection blip, deadlock, a `SaveChanges` timeout — the exception propagates out of
`SendThroughAccountAsync`, out of the switcher, and into `TrySendEmailAsync`'s
`catch (Exception ex)`, which returns `EmailDeliveryResult.Transient(null, ex.Message)`. The row
lands in `errorBag`, becomes `Error` with `EmailSendAttempts + 1`, is re-claimed on a later pass,
and is **sent again** — while `AtsEmailSendLog` may or may not have recorded the first one, so the
quota figure can be wrong in either direction.

The generic catch's own comment ("Transient is the safe reading") is right about lookup failures and
wrong about post-delivery bookkeeping. Note also that the quota log row is written *first* inside
`RecordSuccessfulSendAsync`, so a failure in the second write leaves consumption counted and the
breaker un-reset.

### 10.3 A login throttle is durable for ten minutes, not thirty

**Rate-limit bypass on restart or in a second replica.** This is C1, and the mechanism is worth
spelling out because the two halves are in different files:

- `SmtpConnectionPool.cs:157` parks the **in-memory** limiter for
  `LoginThrottleBackoffSeconds` (1800 s) when `ClassifyConnectFailure` returns `Throttled`.
- `SendOverContextAsync` returns that result to `SendThroughAccountAsync`, which calls
  `ReportFailureAsync`. Its `Throttled` branch, `SmtpAccountPoolRegistry.cs:229`, computes
  `var until = now.AddSeconds(_options.Value.ThrottleBackoffSeconds);` — **600 s** — and writes it
  to `CoolingDownUntil`, then calls `throttledContext.RateLimiter.ReportThrottled(600 s)`.
- `ReportThrottled` never shortens an existing back-off, so the in-memory 30 minutes survives.
  The **row** says 10.

`AtsEmailAccountSnapshot.IsSendable(now)` reads `CoolingDownUntil` from the row. So at minute 11:

- In the same process, the pool's `IsLoginThrottled` still refuses, so nothing is actually sent —
  but `HasSendableAccountAsync` now returns **true**, the pass claims up to 200 rows, and every one
  of them walks the switcher to a `SmtpLoginThrottledException` before the first one trips
  `throttleSignal`. That is 200 claimed-and-deferred rows per tick for nineteen more minutes,
  against a mailbox the provider is still refusing.
- After a restart, or in a second replica with its own fresh in-memory limiter, the refusal is gone
  too. The account is offered messages and performs **real login attempts** into a live provider
  throttle — the exact loop incident 2 describes, re-entered through the durability gap.

The design doc's §3 step 1 explicitly promises the opposite ("so a restart cannot readmit an account
the provider is still throttling"), and §7's claim that "the cooldown is written through to the row,
so a second replica learns about it on its next selection rather than never" is true only for the
ten-minute figure.

### 10.4 `MinSecondsBetweenLogins = 0` silently disables the incident-2 fix

**Rate-limit bypass by configuration.** `SmtpRateLimiter`'s constructor guards two of the three
throughput inputs and not the third:

```csharp
		if (perSecond <= 0)
		{
			perSecond = new AtsEmailDeliveryOptions().MaxSendsPerSecond;
		}

		_minIntervalTicks = TimeSpan.TicksPerSecond / perSecond;

		_minLoginInterval = TimeSpan.FromSeconds(
			Math.Max(0, options.Value.MinSecondsBetweenLogins));
```

`SmtpConnectionPool` does the same for `MaxConcurrentConnections`
(`_options.MaxConcurrentConnections > 0 ? … : new AtsEmailDeliveryOptions().MaxConcurrentConnections`).
`MinSecondsBetweenLogins` is instead clamped to **zero**, so `AtsEmailDelivery:MinSecondsBetweenLogins = 0`
(or any negative) yields a zero `_minLoginInterval` and `WaitForLoginSlotAsync` returns immediately
forever. No fallback, no warning log, no test. Since there is no `AtsEmailDelivery` section in the
repo today (C11) this is latent — it fires the first time somebody adds one and typos the value, or
sets it to `0` meaning "no artificial delay".

The design doc §6 says "Do not add concurrency to go faster … raising the connection count cannot
raise either rate". That is true only while `MinSecondsBetweenLogins` is positive.

### 10.5 `LooksLikeThrottle` runs before the 5xx test, so some permanent refusals cool down the whole rotation

In `ClassifySendFailure` the first branch is
`if (code is 421 or 454 || LooksLikeThrottle(exception.Message))`, and `LooksLikeThrottle` matches
the bare substring `"too many"` — with no code restriction. A provider response such as
`552 5.5.3 Too many recipients in a single message`, or `550 Too many headers`, is therefore
classified `Throttled`, and `EmailDeliveryResult.Throttled` is hardwired to
`EmailFailureScope.Account`.

Consequences, all of them the thing the Message/Account split exists to prevent:

- `CanRetryOnAnotherAccount` → `true`, so the message is re-offered to **every** other registered
  account. Each one refuses identically, because the refusal is about the message.
- Each refusal calls `ReportFailureAsync` with `Throttled`, which cools that account down for
  `ThrottleBackoffSeconds` (10 min) and writes it to the row. One malformed message can park the
  entire rotation for ten minutes.
- The `Message`-scoped 5xx path — the "a `550` must never count" rule the accounts doc §5 calls the
  most important rule in the feature — is bypassed, because the code never reaches the scope
  computation.

`LooksLikeSenderRejection` carries an explicit do-not-widen warning; `LooksLikeThrottle` carries
none, and `"too many"` is the loosest of its four terms.

### 10.6 The exhaustion bell fires every five seconds, and toasts

C9. `RaiseAccountsExhaustedAsync` is called from two places — the early return when no account is
sendable, and after a pass that deferred rows — and both run once per tick. Nothing dedupes:
`AtsNotificationRepository.AddAsync` is a bare `Add` + `SaveChangesAsync`, and the design doc's own
reasoning for raising it per pass rather than per row ("Otherwise a single outage buries the bell
under hundreds of identical entries") applies unchanged across passes.

A single-account deployment that hits its daily cap raises one `EmailAccountsExhausted` per
administrator every 5 seconds until the cooldown lapses: ~120 rows per admin per 10-minute send
throttle, ~360 per 30-minute login throttle. And `NotificationCenter.razor.cs:95` toasts it:

```csharp
	private static bool ShouldToast(string type) => type switch
	{
		AtsNotificationTypes.TicketingFailed => false,
		AtsNotificationTypes.InvitationEmailFailed => false,
		_ => true
	};
```

`EmailAccountsExhausted` falls to `_ => true`, so each of those is also a toast.
`GetAtsAdministratorUserIdsAsync` is cached, so the recipient list is cheap; the inserts are not.

The bulk-completion bell has the smaller version of the same problem: `RaiseForCompletedBulkEmailsAsync`
is called with each pass's `attempted` ids, and a file qualifies whenever `InFlightCount == 0`. A
file whose last row keeps failing is re-claimed on each of its five passes and re-qualifies each
time, so the uploader gets up to five identical "Invitations sent with errors" rows. The
`NotificationCenter` comment that assumes otherwise is "a bulk file finishes once".

### 10.7 The single-enrolment path sends inline, inside a database transaction

C10. `EndorsementSubmissionService.InsertEmailInvitationRequestAsync` wraps the insert, the send,
the status update and the history entry in `TransactionRunner.RunAsync`, and the send is
`SendApplicationFormToUserEmailAsync` → `SendATSEmailWithResultAsync` → the switcher →
`WaitForSlotAsync`. The code's own comment is candid about the SMTP half of the trade-off:

> What that does NOT cover: SMTP is external and cannot be rolled back, so if the send
> succeeds and the commit then fails, the candidate holds a link to an order that
> no longer exists. That window is the price of sending inline; the alternative is
> queueing it for EmailNotificationProcessor, which is how bulk orders work.

What the comment does not mention is that the inline send **queues behind the bulk pass's rate
limiter**. `WaitForSlotAsync` reserves a slot on the account's shared limiter, and the bulk job runs
4 rows in flight at 0.9/s. A single enrolment submitted during a 200-row bulk pass can wait behind
those reservations while holding an open PostgreSQL transaction — and if every account is throttled,
it walks the whole switcher, gets `Throttled`, and `SendApplicationFormToUserEmailAsync` converts
that into `throw new InternalServerException("Failed to send Notification email.")`, rolling the
order back. A candidate enrolment therefore fails outright during a sender outage instead of being
queued, which is the opposite of what the bulk path does.

`SendATSEmailAsync` compounds this by passing `CancellationToken.None` (§5), so the dispute mail and
`SendEmailAsync` callers cannot be cancelled mid-switcher at all.

### 10.8 OTP sends through a registered mailbox are outside that mailbox's budgets

`SendWithCredentialsAsync` builds a throwaway limiter and pool (§5). For **registration** that is
correct — the mailbox is not in rotation yet, and accounts §1.4 explains why nothing may be cached.
For **delete** and **resend-OTP** it is not: `UnprotectStoredPassword` supplies the credentials of an
account that may be actively sending, and the throwaway limiter has never heard of it.

So an operator clicking "delete" on a busy account causes one more `AUTH LOGIN` against that
mailbox, not paced by that mailbox's `MinSecondsBetweenLogins`, not refused by that mailbox's
`IsLoginThrottled`, and not counted by its `SmtpConnectionPool.LoginCount` — the very counter the
design doc §5 calls "the leading indicator". It is one login, so it is small; it is also invisible
to every diagnostic that exists to detect a login burst, and it is exactly the shape of request that
produced incident 2 when it happened 200 times.

### 10.9 The broken pre-fix resend write is still reachable through the interface

§6.3. `IEmailInvitationRepository.ResendApplicationFormAsync` (`:52`) →
`ATSRepository.EmailInvitations.cs:296` → `ATSCacheRepository.EmailInvitations.Cache.cs:88`, with a
cache invalidation wired up for it, and **no production caller**. It is the token-only write the
design doc §8 lists as bug 2 and bug 4 combined: no `EmailSentStatus`, no `EmailSendAttempts` reset,
and no status predicate at all — so unlike `RequeueEmailInvitationAsync` it *will* overwrite the
token of a row that is `Processing` right now, invalidating the link a worker is about to email.

Dead code that implements a known-broken behaviour, on a public interface, with a decorator that
looks after its cache. Anyone wiring up a "resend" from a new surface will find it by name first.

### 10.10 The account snapshot query is on the hot path, once per message per account

`GetNextSendableAccountAsync` opens a DI scope and runs `GetSnapshotsAsync` — every account, each
with a correlated `SUM(RecipientCount)` over `ats."EmailSendLog"` (accounts §4.5) — and it is called
once by `HasSendableAccountAsync`, then once per switcher iteration per message. A 200-row pass with
no failover is ~201 of those queries; with two accounts and frequent failover it is up to ~400.

`IAtsEmailAccountRepository` is deliberately uncached (`ATSServiceConfiguration.cs:137`, with the
reasoning quoted in accounts §6), so this is by design and correct — a cached view of which account
is healthy routes mail to a mailbox that is already cooling down. It is noted because it is the
first thing to look at if a pass slows down, and because the "uncached" decision and the "one query
per message" cost are recorded in two different documents that do not mention each other.

### 10.11 The send-log sweep is a single unbounded delete

§8.1. `DeleteSendLogsOlderThanAsync` is one `ExecuteDeleteAsync` over everything older than the
cutoff, with no `Take(batchSize)` loop, on an hourly timer, with no enable flag. At 0.9/s per
account continuously that is ~78 000 rows/day per account, so the steady-state hourly delete is
~3 200 rows — fine. The bad case is the first run after the feature is enabled on an existing
database, or after the host has been down for a while: one long-running `DELETE` holding row locks
on the table the quota `SUM` reads, i.e. on the hot send path. Both sibling retention services batch
for exactly this reason.

### 10.12 Two smaller ones

**A cancelled waiter consumes a slot it never used.** `WaitForSlotAsync` advances `_nextSlotUtc`
under the gate and *then* awaits `Task.Delay(delay, cancellationToken)`. A caller cancelled during
that delay has reserved a slot nobody sent on, pushing every later caller out by one interval.
Bounded by the in-flight count (4), so it costs fractions of a second per stand-down — but it means
the limiter's accounting and the number of messages actually sent diverge slightly after every
throttle trip and every shutdown.

**`ReportThrottled` blocks a thread-pool thread.** It calls `_gate.Wait()`, not `WaitAsync`, and is
called from `SmtpConnectionPool.CreateConnectionAsync` and `SmtpAccountPoolRegistry.ReportFailureAsync`,
both async. The gate is never held across an await so it cannot deadlock; it is still sync-over-async
on a hot path, and it is the only such call in the class.

---

## 11. Wiring

### 11.1 DI — `BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs`

Everything the delivery path needs, with the line it is on:

```csharp
		services.AddSingleton<ISmtpAccountPoolRegistry, SmtpAccountPoolRegistry>();          // 132
```
```csharp
		services.AddScoped<IAtsEmailAccountRepository, AtsEmailAccountRepository>();         // 137
```
```csharp
		services.AddHostedService<AtsEmailSendLogRetentionService>();                        // 142
```
```csharp
		services.AddKeyedScoped<IEmailService, ATSEmailService>("ats");                      // 148
```
```csharp
		services.AddScoped<IAtsEmailSender>(provider =>
			(IAtsEmailSender)provider.GetRequiredKeyedService<IEmailService>("ats"));        // 155-156
```
```csharp
		services.AddScoped<IEmailNotificationProcessorService, EmailNotificationProcessorService>();  // 158
```
```csharp
		services.ConfigureOptions<EmailNotificationBackgroundJobSetup>();                    // 170
```
```csharp
		services.Configure<AtsEmailDeliveryOptions>(
			configuration.GetSection(AtsEmailDeliveryOptions.SectionName));                  // 226-227
```

**`SmtpConnectionPool` and `SmtpRateLimiter` are not registered at all**, and must not be — the
registration comment above line 132 is the reasoning, quoted in full in accounts §5.1. Ask
`ISmtpAccountPoolRegistry.GetContextAsync(accountId)` for an account's pair.

The lifetime split is the thing to hold in mind: the **registry is a singleton** (it owns the pools,
limiters, lease counts and throttle windows), and **everything that touches it is scoped** —
`ATSEmailService`, `EndorsementSubmissionService`, `EmailNotificationProcessorService`,
`AtsEmailAccountRepository`. That is why the registry takes `IServiceScopeFactory` and opens a scope
per database touch, and why the processor opens a scope per send attempt.

`IATSRepository` is decorated (`services.Decorate<IATSRepository, ATSCacheRepository>()`, `:47-48`)
and `IEmailInvitationRepository` is one of the forwarding registrations
(`:56`). The queue methods forward **without** caching:

```csharp
	public async Task<List<EmailInvitationRequest>> GetPendingEmailInvitationRequestsAsync()
	{
		return await _atsRepository.GetPendingEmailInvitationRequestsAsync();
	}
```

so the decorator is a pass-through for the whole claim/release/update surface
(`ATSCacheRepository.EmailInvitations.Cache.cs:15-58`). Caching a queue would be the bug the
ticketing companion §8.1 warns about; here it is avoided by simply not caching, inside a class whose
job is caching.

**The duplicate keyed registration.** `EmploymentVerificationServiceConfiguration.cs:57`:

```csharp
		services.AddKeyedScoped<IEmailService, ATSEmailService>("ats");
```

with `using ATS.Services.EmailService;` at the top of that file. `AddModuleServices`
(`BackendAPI/API/APIs/ServiceConfig/ServiceConfiguration.cs:327-341`) calls `AddATSServices()` at
`:335` and `AddEmploymentVerificationServices()` at `:337`, so the **EmploymentVerification
descriptor is the last one for key `"ats"`** and is what
`GetRequiredKeyedService<IEmailService>("ats")` resolves — including the forwarding registration at
`ATSServiceConfiguration.cs:155` that produces `IAtsEmailSender`. Both descriptors name the same
implementation type, so behaviour is identical today; if either module ever swapped its
`IEmailService`, the ATS sender would silently change identity with nothing in the ATS module
indicating why. This is not mentioned in any existing document.

### 11.2 Quartz

`AddATSInfrastructure` (`:211-283`):

```csharp
			q.SchedulerId = "ATS";                                                            // 249
```
```csharp
			q.UsePersistentStore(options =>
			{
				options.UsePostgres(postgres =>
				{
					...
					postgres.TablePrefix = "ats.qrtz_";
				});

				options.UseProperties = true;

				options.UseNewtonsoftJsonSerializer();

				options.UseClustering();
			});
```
```csharp
		services.AddQuartzHostedService(options =>
		{
			options.WaitForJobsToComplete = true;                                            // 278
		});
```

The fixed `SchedulerId` with `UseClustering()` is the design doc §7's known limitation, and it is
accurate: N replicas share one identity in `qrtz_scheduler_state`, and the limiters are per process
per account, so N replicas send at N × the configured rate for each account. `WaitForJobsToComplete = true`
is what makes a graceful shutdown reach §10.1 rather than simply killing the pass.

### 11.3 Gateway routes — `BackendAPI/Modules/ATS/Path/ATSPaths.cs:602-622`

```csharp
			new RouteDefinitionDTO(
				RouteId: "ResendApplicationForm",
				MatchPath: "/ats/resendapplicationform",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/resendapplicationform" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "ResendApplicationForms",
				MatchPath: "/ats/resendapplicationforms",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/resendapplicationforms" }
				}
			),
```

Both `PATCH`, both `PathSet` (no path segments — the ids travel in the body), and the two route ids
differ by one character. Each resend route is **three independent string literals in three
assemblies** that must agree with nothing enforcing it: the Carter `MapPatch("resendapplicationform[s]")`,
the `MatchPath`/`PathSet` here, and the `_httpClient.PatchAsJsonAsync("ats/resendapplicationform[s]")`
in the UI service. `GET /__routes` is the runtime check.

Note the near-miss: `resendapplicationform` is a **prefix** of `resendapplicationforms`. YARP matches
`MatchPath` exactly rather than by prefix, so the two do not collide — but a typo'd `MatchPath` here
fails as a 404 from the gateway, not as a routing conflict, which is the less diagnosable of the two.

The background job, the pool, the limiter and the retention sweep have no routes: nothing about the
delivery path is reachable over HTTP except the two resend slices.

---

## 12. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| A `EmailStatus` constant (`ATS/Constants/EmailStatus.cs`) | `SubjectEmailSentStatus` in `UI/FrontendWebassembly/DTO/ATS/BulkUploadDashboardDTO.cs:156`, the badge mapping in `BulkUploadSubjectsDialog.razor.cs:611-621`, and the claim query's `{2}`/`{3}` bindings | Four hand-synced copies across two assemblies; the class is `internal` so the UI cannot reference it (§4, §9.4) |
| The claim SQL's placeholder order | The `FromSqlRaw` argument list at `ATSRepository.EmailInvitations.cs:53-59` | Positional `{0}`–`{6}`; `{0}`/`{1}` are the *written* values and appear first in the args but last in the SQL (§1.3) |
| The literal `200` batch cap | `StaleClaimTimeout` (30 min) and `MaxBulkResendSize` (500) | The sweeper must outlast the worst-case pass (200 ÷ 0.9/s ≈ 3.7 min, plus a 10-min back-off) or it steals live rows and duplicates sends; a 500-row requeue needs ≥3 ticks to drain (§1.3, §6.2) |
| `MaxSendsPerSecond` | `MaxBulkResendSize`'s comment ("about nine minutes of sending"), `StaleClaimTimeout`'s comment ("200 messages … roughly four minutes"), and the design doc §4's 3.7-minute figure | Three places quote arithmetic derived from 0.9 (§2.1, §6.2) |
| `MinSecondsBetweenLogins` | `SmtpRateLimiter`'s constructor | It is the one throughput input with **no** fallback for a non-positive value — `Math.Max(0, …)` silently disables login pacing (§10.4) |
| `ThrottleBackoffSeconds` or `LoginThrottleBackoffSeconds` | `SmtpConnectionPool.cs:157` **and** `SmtpAccountPoolRegistry.cs:229` | Only the pool reads the login value, and only in memory; the row always gets the send value. Changing either without the other widens or closes the durability gap (§10.3) |
| `SmtpFailureClassifier.LooksLikeThrottle` | The order of the branches in `ClassifySendFailure` | The throttle test runs before the 5xx/scope test, so any new substring can promote a message-scoped refusal to an account-scoped throttle and park the whole rotation (§10.5) |
| `SmtpFailureClassifier.LooksLikeSenderRejection` | `AtsEmailAccountBreakerTests` and accounts §5 | Its `<remarks>` forbids widening it; widening puts recipient failures back in the account bucket (§3.1) |
| `EmailDeliveryResult.CanRetryOnAnotherAccount` | `ATSEmailService.cs:113` (the switcher's continuation test) and every `EmailDeliveryResult.Transient(…, EmailFailureScope.Account)` construction | Including `Transient` is the duplicate-invitation bug: a transient can fire after the provider accepted the message (§3.4) |
| The `Transient`/`Permanent`/`Throttled` factories' default `Scope` | `SmtpFailureClassifier`, `SendOverContextAsync`'s three catch blocks, `ReportFailureAsync`'s `IsAccountFault` gate | `Scope` defaults to `Message`; forgetting to pass `Account` silently stops the breaker counting a failure (§3.2, §3.3) |
| `SendThroughAccountAsync`'s post-send reporting | `TrySendEmailAsync`'s `catch (Exception ex)` | Any throw from `ReportSuccessAsync`/`ReportFailureAsync` is read as a retryable delivery failure and re-sends a delivered message (§10.2) |
| The `Task.Delay` or `semaphore.WaitAsync` in the processor | §10.1 | Both are unguarded; an escaping `OperationCanceledException` skips every status write and strands sent rows in `Processing` |
| `MaxAttemptsPerPass` or `MaxEmailSendAttempts` | The design doc §2's "up to 15 attempts" and §5's `EmailSendAttempts = 5` diagnostic | `EmailSendAttempts` counts **passes**, not SMTP attempts; the product of the two is the real ceiling (§2.4) |
| `inFlightLimit`'s derivation | `MaxConcurrentConnections` | It is `MaxConcurrentConnections * 2`, so a config change moves the fan-out with no code edit — unlike the ticketing job's constant (§7) |
| Anything in `RequeueEmailInvitationAsync`'s setter list | `RequeueEmailInvitationsAsync` (the bulk form, `:108`) and the design doc §8's field table | Two near-identical setter lists with no shared code; the doc's table already omits `HashTokenCreatedAt` (C6) |
| The requeue's `WHERE` predicate | `ResendApplicationFormIntegrationTests` (`…ShouldThrowConflict_WhenTheInvitationIsMidSend`, `…ShouldSkipInvitationsThatAreMidSend`) | The predicate *is* the concurrency guard; `Processing` is the only exclusion and both tests pin it (§6.1) |
| `ATSCacheRepository.EmailInvitations.Cache.cs` tag invalidation | `CacheTags.Report` and `CacheTags.WithdrawnApplication` | The queue methods deliberately do **not** invalidate; the requeue methods invalidate both. Adding a cache to a queue method is the bug (§11.1) |
| A Carter route string (`MapPatch("resendapplicationforms")`) | `PathSet` in `Path/ATSPaths.cs:614-622` **and** the URL literal in `UI/.../Services/ATS/EndorsementSubmission/EndorsementSubmissionService.cs:258` | Three independent literals in three assemblies (§11.3) |
| Either resend endpoint's response shape | The UI's `ReadFromJsonAsync<bool>` vs `ReadFromJsonAsync<BulkRetryResultDTO>` | Single returns a **bare bool** (`Results.Ok(response.Success)`), bulk returns the record. A mismatch is a silent deserialisation failure (§9.5) |
| `BulkRetryResultDTO` | `UI/FrontendWebassembly/DTO/ATS/BulkRetryResultDTO.cs` | Two independent types; the backend's `IsComplete` is a computed getter, the UI's is settable (§6.2) |
| `AtsEmailSendLog`'s columns or its index | `GetConsumedInWindowAsync`, `BuildSnapshotQuery` (accounts §4.5), `DeleteSendLogsOlderThanAsync`, `AtsEmailSendLogRetentionService.SweepAsync`'s `Math.Max` | The retention floor exists so the sweep cannot erase a row the quota `SUM` still needs (§8.1) |
| `SendLogRetentionHours` or `QuotaWindowHours` | The `Math.Max(QuotaWindowHours + 1, SendLogRetentionHours)` in `SweepAsync` | Lowering retention below the quota window would understate consumption and walk into the provider's cap (§8.1) |
| The keyed `"ats"` registration in either module | `EmploymentVerificationServiceConfiguration.cs:57` and the registration order in `ServiceConfiguration.cs:335-337` | Two descriptors for one key; the last registered wins, and EV registers second (§11.1) |
| `RaiseAccountsExhaustedAsync` or `RaiseForCompletedBulkEmailsAsync` | `ShouldToast` in `NotificationCenter.razor.cs:97` and the absence of any dedupe in `AtsNotificationRepository.AddAsync` | Both fire once per **tick**, and `EmailAccountsExhausted` also toasts (§10.6) |
| `StaleClaimTimeout` | `ReleaseStaleEmailInvitationClaimsAsync`'s lack of a `CancellationToken`, and §10.1 | The sweeper is the only thing that recovers a row stranded in `Processing`, and it does not charge an attempt — so anything that strands a *sent* row causes a resend |

---

## When to update this document

Whenever a step is inserted into the §1 chain, a call moves to a different file, a classification
rule changes, or a DI registration changes, update the relevant section. If a change is additive (a
new send entry point following the existing pattern), add a row to the §5 table rather than a new
walkthrough. §0 rows should be **removed** when the design doc is corrected, not left to accumulate.
This document may go stale on prose explanations of *why* (that drifts slower); it must not go stale
on *which file calls which*, or on the numbers in §2.1 — those are the reasons it exists.
