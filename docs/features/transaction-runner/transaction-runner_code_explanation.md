# TransactionRunner — Code Explanation

Companion to [`transaction-runner.md`](transaction-runner.md). That document explains *why* the
runner exists and which helper to reach for; this one is for someone about to change it or migrate a
service onto it. It quotes the whole runner, names the real method at each hop of the two call
chains, and states the behaviour the code has rather than the behaviour the design doc describes.
*"I'm changing X, what else touches it?"* is the last table.

> **Read §0 first.** Everything below was verified against the code on branch
> `feature/Update-ReadMe-File`. The subject is two files — `ITransactionScope.cs` (21 lines) and
> `TransactionRunner.cs` (140 lines), the only two in
> `BackendAPI/BuildingBlocks/BuildingBlocks/Data/` — with exactly **two** production call sites, so
> the value here is precise semantics, not surface area.

---

## 0. Where the design doc no longer matches the code

| # | `transaction-runner.md` says | The code actually does |
|---|---|---|
| **C1** | §6: "Already migrated: `EndorsementSubmissionService.cs` (both `InsertEmailInvitationRequestAsync` **and `ResendApplicationFormAsync`**)" | **Only the first.** `ResendApplicationFormAsync` (`EndorsementSubmissionService.cs:449`) has no `TransactionRunner` call — `RequeueEmailInvitationAsync` (`:507`) then `RecordAsync` (`:526`) commit independently. `ResendApplicationFormsAsync` (`:538`) likewise. Both unmigrated. |
| **C2** | §2: the `RunAsync` delegate holds three statements | It holds **four.** `UpdateSingleEmailInvitationRequestStatusForSentEmailAsync` (`:183`) is missing from the doc's example, and it is the interesting one — an `ExecuteUpdateAsync`, its own SQL round trip past the change tracker (§5.1). |
| **C3** | §2 presents `RunAsync<T>` and the multi-compensation overload as working API | Both exist (`TransactionRunner.cs:56`, `:112`) but have **zero production call sites.** `RunAsync<T>` is never called; the list overload is reached only from the single-compensation one. **NOT FOUND** as exercised code. |
| **C4** | §2 groups `RunWithCompensationAsync` under "the three methods" of a document about transactions | It **opens no transaction.** No `Begin`/`SaveChanges`/`Commit`/`Rollback` anywhere in it, and it takes no `ITransactionScope`. It is a try/catch-with-cleanup helper sharing a class name. |
| **C5** | §6 lists five files that still hand-roll the block | Those five are confirmed (six call sites), **plus one the doc omits**: `ATS/Data/Repository/Clients/ATSRepository.Clients.cs:64`, `AddClientAsync` — raw `await using var transaction = await _dbcontext.Database.BeginTransactionAsync(...)` with **no `try`/`catch` at all** and two `SaveChangesAsync` calls. |
| **C6** | §6 item 1 warns about a repository `SaveChangesAsync` running *before* `BeginTransactionAsync` | True but incomplete. The repository methods inside the delegate also flush **themselves**, so the runner's own `SaveChangesAsync` (`TransactionRunner.cs:35`) is a **no-op at the only call site that exists** — everything is already on the server, inside the open transaction, before `CommitAsync` (§5.1). |

One doc claim that is **still exactly right**, and was worth checking: §5's *"ATS is wired up.
PhilSys is not yet."* Verified in §4.

---

## 1. `ITransactionScope.cs`, whole file

```csharp
namespace BuildingBlocks.Data;

/// <summary>
/// The transaction primitives <see cref="TransactionRunner"/> drives.
/// </summary>
/// <remarks>
/// Each module already has its own <c>IUnitOfWork</c> with exactly these four members
/// (ATS, PhilSys). Rather than move those to a shared type - which would touch every
/// service that injects one - each module's interface simply declares that it implements
/// this, and the shared runner works against it.
/// </remarks>
public interface ITransactionScope
{
	Task BeginTransactionAsync(CancellationToken ct = default);

	Task SaveChangesAsync(CancellationToken ct = default);

	Task CommitAsync(CancellationToken ct = default);

	Task RollbackAsync(CancellationToken ct = default);
}
```

**`BuildingBlocks.csproj` has no Entity Framework reference at all** — only Carter,
FluentValidation, Mapster, MediatR, FeatureManagement, SemanticKernel, `System.IO.Packaging` and the
Aliyun OSS SDK. That is why this interface exists instead of the runner taking a `DbContext`: the
shared layer cannot name an EF type, so a transaction is described as four `Task` methods and each
module supplies the EF implementation (§4). Neither file has a `using` directive; `ImplicitUsings`
is enabled.

---

## 2. `RunAsync` — the whole implementation

`TransactionRunner.cs:24-49`, verbatim:

```csharp
	public static async Task RunAsync(
		ITransactionScope unitOfWork,
		Func<Task> work,
		CancellationToken cancellationToken = default)
	{
		await unitOfWork.BeginTransactionAsync(cancellationToken);

		try
		{
			await work();

			await unitOfWork.SaveChangesAsync(cancellationToken);

			await unitOfWork.CommitAsync(cancellationToken);
		}
		catch
		{
			// Release the transaction, then let the original exception continue to the
			// global handler untouched. Nothing is logged or wrapped here - doing either
			// would hide which exception type was thrown and cost the caller its status
			// code.
			await unitOfWork.RollbackAsync(cancellationToken);

			throw;
		}
	}
```

Three shape decisions are deliberate and easy to "fix" by accident. **`BeginTransactionAsync` is
outside the `try`**: if beginning throws there is nothing to roll back. **`CommitAsync` is inside
it**, so a commit failure — deferred constraint, serialisation conflict, dropped connection — takes
the rollback path instead of escaping. **`SaveChangesAsync` sits between work and commit**, so a
delegate that only stages changes in the tracker still gets them written; at the one live call site
that never happens (C6).

`RunAsync<T>` (`:56-79`) is the same method with three differences: `var result = await work();`
(`:65`), `return result;` (`:71`) placed **after** `CommitAsync`, and an identical bare
`catch`/`RollbackAsync`/`throw;` (`:73-78`). The ordering is the whole guarantee — `return result`
is unreachable unless the commit returned, so a caller can never hold a value whose transaction was
rolled back. No flag, no `finally`, no `out` parameter; purely control flow. It has no callers (C3).

### 2.1 The exact semantics of the `catch`

The doc's central claim — *"not error handling; rethrown untouched"* — is **literally true**, and
three separate things in the code prove it.

**It is a bare `catch`, not `catch (Exception ex)`.** No exception variable is bound, so there is
nothing available to log, inspect, wrap or filter on — the block cannot *become* error handling
without a signature change.

**It rethrows with `throw;`, not `throw ex;`** — the distinction that matters. `throw;` re-raises
the original object and **preserves its stack trace**, so the trace still points at the line inside
the delegate that failed. `throw ex;` would reset it to the `throw` statement and every failure would
appear to originate at `TransactionRunner.cs:47`. All three methods use bare `throw;`; there is no
`throw ex;` in the file.

**Nothing else happens in the block.** One `await`, one `throw`. `TransactionRunner` is a
`static class` with no fields and no injected logger, so it *cannot* log even if someone wanted it
to.

The payoff is `BuildingBlocks/Exceptions/Handler/CustomExceptionHandler.cs`, whose `switch`
expression maps by **runtime type** — its `NotFoundException =>` arm sets `Status404NotFound`, its
terminal `_ =>` arm sets 500. Because the runner rethrows the same object, a `NotFoundException` from
inside a delegate arrives as a `NotFoundException` and produces a 404. Wrap it in
`InternalServerException` and that arm fires instead — a 500 for a missing row. That is exactly what
the unmigrated PhilSys code still does (§5.3).

---

## 3. `RunWithCompensationAsync` — no transaction involved

The single-compensation overload (`:95-98`) is a forwarding one-liner; note what its signature
lacks — no `ITransactionScope`, no `CancellationToken`. All the work is in the list overload,
`:112-139`:

```csharp
	public static async Task RunWithCompensationAsync(
		Func<Task> work,
		IReadOnlyList<Func<Task>> compensations,
		Action<Exception>? onCompensationFailed = null)
	{
		try
		{
			await work();
		}
		catch
		{
			foreach (var compensate in compensations)
			{
				try
				{
					await compensate();
				}
				catch (Exception compensationException)
				{
					// Reported, never rethrown: the original failure is the one the caller
					// needs, and a broken cleanup must not stop the cleanups after it.
					onCompensationFailed?.Invoke(compensationException);
				}
			}

			throw;
		}
	}
```

**What "compensation" means here.** A database transaction can only undo database writes. If the
work already put a blob in object storage or created a row in a third-party system, no `ROLLBACK`
reaches it — the orphan stays forever unless something deletes it explicitly. The compensation *is*
that delete: a manual undo for a resource the transaction never enlisted. It runs **only on
failure**; the success path falls out of the `try` with no cleanup. A caller needs it exactly when a
non-transactional resource was acquired *before* a database write that can still fail — §5.2 is the
only real case.

Two properties of the loop carry the correctness. **Each compensation gets its own `try`**, so they
run in list order and one that throws is reported while the loop continues; collapsing them into one
delegate with several `await`s would share a single `try`, and the first failure would silently skip
every cleanup after it — the bug this overload exists to prevent. And **`onCompensationFailed` never
rethrows**: the original exception always wins via the bare `throw;` at `:137`, because "could not
delete the blob" is strictly less useful to the caller than the insert failure that caused it. The
callback is nullable and optional — omit it and a failed compensation is **silently dropped**, since
the class has no logger to fall back on.

---

## 4. `IUnitOfWork` — the contract, and who implements it

`ATS/Data/UnitOfWork/IUnitOfWork.cs` is an **empty body** — `public interface IUnitOfWork :
ITransactionScope { }` plus XML remarks. The inheritance *is* the adoption, and no service injecting
`IUnitOfWork` had to change.

**PhilSys has not adopted it.** `PhilSys/Data/UnitOfWork/IUnitOfWork.cs` opens:

```csharp
namespace PhilSys.Data.UnitOfWork;

public interface IUnitOfWork
{
```

and then declares all four members itself, character for character the same signatures as
`ITransactionScope`, same `= default` on each. But there is no `: ITransactionScope`, so
`PhilSys.Data.UnitOfWork.IUnitOfWork` is not assignable to the runner's parameter and
`TransactionRunner.RunAsync(_unitOfWork, ...)` **will not compile** in that module today. Adopting it
is adding the base interface and deleting the four declarations — the doc's "one-line change" claim
is accurate. A repo-wide grep for `ITransactionScope` returns ten hits: two in the runner, two in
ATS's interface, six in documentation. **No module other than ATS references it.**

Both `UnitOfWork` implementations (`ATS/Data/UnitOfWork/UnitOfWork.cs`,
`PhilSys/Data/UnitOfWork/UnitOfWork.cs`) are otherwise identical, and both are registered
`services.AddScoped<IUnitOfWork, UnitOfWork>()` (`ATSServiceConfiguration.cs:99`,
`PhilSysServiceConfiguration.cs:57`). The two methods that matter for §6 — `BeginTransactionAsync`
(`ATS/Data/UnitOfWork/UnitOfWork.cs:13-16`) and the body of `RollbackAsync` (`:35-40`):

```csharp
	public async Task BeginTransactionAsync(CancellationToken ct = default)
	{
		_transaction = await _atsDBContext.Database.BeginTransactionAsync(ct);
	}
```

```csharp
		if (_transaction != null)
		{
			await _transaction.RollbackAsync(ct);
			await _transaction.DisposeAsync();
			_transaction = null;
		}
```

`_transaction` is one `IDbContextTransaction?` field. `Begin` assigns unconditionally; `Commit` and
`Rollback` both null-guard and null-out, which makes a second call a **silent no-op** — usually a
feature, in §6 a way to lose an error.

---

## 5. Every real call site

A repo-wide grep for `TransactionRunner` outside `docs/` and `README.md` returns **four** hits, all
in one file: two calls and two comments. That is the complete list.

| # | Method | File:line | Helper |
|---|---|---|---|
| 1 | `EndorsementSubmissionService.InsertEmailInvitationRequestAsync` | `ATS/Services/EndorsementSubmission/EndorsementSubmissionService.cs:170-192` | `RunAsync` |
| 2 | `EndorsementSubmissionService.InsertBulkSubjectAsync` | same file, `:269-275` | `RunWithCompensationAsync`, single compensation |

### 5.1 Call site 1 — what is being made atomic

```csharp
		await TransactionRunner.RunAsync(
			_unitOfWork,
			async () =>
			{
				await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest);

				await SendApplicationFormToUserEmailAsync(
					emailInvitationRequestDTO.EmailAddress!,
					subjectName,
					applicationFormLink,
					emailInvitationRequest.Requestor,
					emailInvitationRequest.ClientId);

				await _atsRepository.UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(
					emailInvitationRequest.EmailInvitationID);

				await _orderHistoryService.RecordAsync(
					emailInvitationRequest.EmailInvitationID,
					OrderHistoryEventType.OrderCreated,
					null,
					OrderStatus.PendingCandidateInfo, ct, source);
			},
			ct);
```

Why this cannot be one `SaveChanges`: it writes **three tables through three collaborators**, and the
middle step is an SMTP send that decides whether the other two should exist at all.

`_atsRepository` is `IATSRepository`, registered at `ATSServiceConfiguration.cs:47-48` as
`ATSRepository` then `Decorate<IATSRepository, ATSCacheRepository>`.
`AddEmailInvitationRequestAsync` (`ATSRepository.EmailInvitations.cs:14-19`) is `AddAsync`
**followed by its own `_dbcontext.SaveChangesAsync()`** — no token, and it flushes the INSERT
immediately. `_orderHistoryService.RecordAsync` is a one-liner forwarding to `_repository.AddAsync`,
and `IOrderHistoryRepository` resolves to *the same* `IATSRepository` instance
(`ATSServiceConfiguration.cs:58`); `ATSRepository.OrderHistory.cs:5-9` is again `AddAsync` +
`SaveChangesAsync(cancellationToken)` — a second independent flush, to `OrderStatusHistories`.
`UpdateSingleEmailInvitationRequestStatusForSentEmailAsync`
(`ATSRepository.EmailInvitations.cs:240-248`) is neither: an `ExecuteUpdateAsync` setting
`EmailSentStatus = Done` and `EmailSentAt = DateTime.UtcNow`, emitting its own `UPDATE` straight to
the connection past the tracker. It still enlists, because it runs on the same `ATSDBContext`
connection — but it is on the server the moment it returns.

So when `TransactionRunner.cs:35` runs, the tracker is empty and that `SaveChangesAsync` is a no-op
(C6). **The transaction, not `SaveChanges`, is what makes this atomic**: all three writes ride one
connection inside one `IDbContextTransaction`, opened before the first and committed after the last.
That is precisely the bug the runner exists for — the order used to commit inside
`AddEmailInvitationRequestAsync` on its own, before the email was even attempted, so a failed send
left a saved order whose candidate never received a link.

### 5.2 Call site 2 — compensation for a blob

```csharp
		await TransactionRunner.RunWithCompensationAsync(
			work: () => _atsRepository.AddBulkUploadFileDetailsAsync(bulkUploadFileDetails),
			compensate: () => _objectStorageService.DeleteAsync(bulkFileKey, ct),
			onCompensationFailed: exception => _logger.LogError(
				exception,
				"Failed to delete the orphaned bulk upload {FileKey} after its row insert failed.",
				bulkFileKey));
```

`InsertBulkSubjectAsync` uploads the CSV to object storage at `:242` (`bulkFileKey = await
_objectStorageService.UploadAsync(...)`) **before** this call, and an upload is not enlistable: once
the bytes are in the bucket they stay there whatever the database does. `work` is
`AddBulkUploadFileDetailsAsync` (`ATSRepository.BulkUploads.cs:8-13`, again `AddAsync` +
`SaveChangesAsync`); if that INSERT fails — a unique violation on the file name, a dropped
connection — the compensation deletes the blob. Without it every failed insert leaks one orphaned
file per attempt, forever, with no row anywhere pointing at it.

Note the asymmetry with call site 1: here the non-transactional resource is acquired *first* and
compensated *afterwards*. There is no `RunAsync` wrapper and no `ITransactionScope` — the single
INSERT is its own implicit transaction inside `SaveChangesAsync` (C4).

### 5.3 What does **not** use it

Everything else still hand-rolls the block. Six `_unitOfWork.BeginTransactionAsync` call sites across
the five files the doc lists — `ApplicationFormService.cs:91` and `:496`, `ReportService.cs:78`,
`DisputeOrderService.cs:154`, `ApplicantSearchProjectionService.cs:35`,
`UpdateFaceLivenessSessionService.cs:86` (`FinalizeInquiryAsync`) — plus the one the doc misses (C5),
`ATSRepository.Clients.cs:64`, which relies entirely on `DisposeAsync` rolling back an uncommitted
transaction.

The PhilSys one is the doc's failure mode #2 live in the code: its `catch (Exception ex)` at
`UpdateFaceLivenessSessionService.cs:105-107` rolls back, logs, then ends in
`throw new InternalServerException($"Failed to add transaction. {ex.Message}");`. Every failure —
including a `NotFoundException` from `AddConvertedResponseToDbAsync` — becomes an
`InternalServerException`, so `CustomExceptionHandler` answers 500 and the original type is gone.
Migrating it fixes the status codes *and* changes them, which is the caveat in the doc's §6.

---

## 6. Nested transactions

**Nothing guards against nesting — not the runner, not `UnitOfWork`.** `RunAsync` calls
`BeginTransactionAsync` unconditionally, with no re-entrancy counter, no `AsyncLocal` depth flag, and
no "already open, just run the work" branch. `UnitOfWork.BeginTransactionAsync` assigns
`_transaction` unconditionally, with no `if (_transaction != null)` check — unlike its `Commit` and
`Rollback` siblings, which both have one.

What actually happens when a `RunAsync` runs inside another on the same scoped `IUnitOfWork` (both
`AddScoped`, and `ATSDBContext` is `AddDbContext` at `ATSServiceConfiguration.cs:234`, so a service
and the repository it calls in one request share one instance):

1. The inner `BeginTransactionAsync` reaches Npgsql with a transaction already open on the
   connection. EF Core's relational connection refuses it: `InvalidOperationException`, *"A
   transaction is already in progress; nested/concurrent transactions aren't supported."* Nothing is
   assigned to `_transaction`, because the throw happens inside the awaited expression.
2. The **inner** runner's bare `catch` calls `unitOfWork.RollbackAsync()` — on the *shared* unit of
   work, whose `_transaction` still points at the **outer** transaction. The inner rollback rolls
   back and disposes the outer transaction and nulls the field.
3. The inner `throw;` propagates to the outer delegate, so the **outer** `catch` fires and calls
   `RollbackAsync` again. `_transaction` is now `null`, the guard skips the body, and the call is a
   **silent no-op**. The outer never learns its transaction was disposed by someone else.
4. The outer `throw;` re-raises the original `InvalidOperationException`, which has no arm in
   `CustomExceptionHandler`'s switch and falls to `_ =>` — a **500** whose message is EF's, not the
   domain's.

Net: the data is safe (everything rolled back), but the caller gets an infrastructure 500, the
rollback that "worked" was performed by the wrong layer, and one of the two rollbacks did nothing. If
EF ever permitted the nested begin — or someone "fixed" `BeginTransactionAsync` to swallow the error
— step 1 would instead **overwrite `_transaction`** and orphan the outer `IDbContextTransaction`.
There is no savepoint support anywhere in the two files. **Do not call `TransactionRunner` from a
method that may itself run inside a `TransactionRunner` delegate.** Both current call sites are
top-level service methods reached straight from a handler, which is why this has never fired.

---

## 7. The side-effect boundary

A rollback undoes database writes and **nothing else**. Any non-database effect inside the delegate
has already happened by the time the transaction aborts, and no compensation runs — `RunAsync` has no
compensation parameter. Call site 1 puts two such effects inside the transaction.

**The SMTP send.** `SendApplicationFormToUserEmailAsync` throws `InternalServerException` when
`result.IsSent` is false, which rolls the order back — that direction is intended. The opposite
direction is the hazard, and the comment at `:163-166` says so:

```csharp
		// does NOT cover: SMTP is external and cannot be rolled back, so if the send
		// succeeds and the commit then fails, the candidate holds a link to an order that
		// no longer exists. That window is the price of sending inline; the alternative is
		// queueing it for BulkEmailNotificationProcessor, which is how bulk orders work.
```

Concretely: the email leaves the server, then `RecordAsync` violates a constraint, then `CommitAsync`
fails — the candidate holds an application-form link pointing at a `HashToken` that was rolled back
and does not exist. Nothing detects it, nothing retries it. The bulk path avoids this by queueing the
row as `EmailStatus.Pending` for `BulkEmailNotificationProcessor` to deliver after the commit; the
single-order path accepts the window deliberately.

**The cache invalidation.** `_atsRepository` is the *decorated* repository, so
`AddEmailInvitationRequestAsync` goes through `ATSCacheRepository.EmailInvitations.Cache.cs:5-13`:
it calls the inner repository, then `if (result) await _hybridCache.RemoveByTagAsync(CacheTags.Report);`.
`result` is `true` as soon as the INSERT flushes — **before the commit** — so a transaction that
later rolls back has still revoked the `Report` tag. Benign here (a spurious cache miss; the cached
page never showed uncommitted data), but the same shape as the SMTP problem, and not benign if the
revocation were expensive or user-visible. The rule: **anything a decorator or repository does for
effect rather than for data escapes the rollback.** An OMS stored-procedure call inside a delegate
would be the worst version — it writes to a remote legacy SQL Server no Postgres transaction can
reach, and unlike §5.2 the `RunAsync` path has no compensation hook. Neither current call site does
this; nothing in the runner stops a future one.

**Cancellation is not propagated.** `RunAsync` threads `cancellationToken` into the four unit-of-work
calls and nowhere else, and the §5.1 delegate drops the token three times:
`AddEmailInvitationRequestAsync` takes no `CancellationToken` and calls `SaveChangesAsync()` with
none; `UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(Guid)` passes none to
`ExecuteUpdateAsync`; `SendApplicationFormToUserEmailAsync` has no token parameter at all, forwarding
`CancellationToken.None` to `SendApplicationFormToUserEmailWithResultAsync`. So an aborted request
does not cancel the SMTP send — it runs to completion while holding the transaction, and its locks on
the just-inserted order row, open for the whole SMTP timeout.

**`SideEffectGuard` runs *inside* the delegate.** The doc presents the two helpers as opposites, and
on failure behaviour they are — but at call site 1 they are also composed:
`SendApplicationFormToUserEmailAsync` → `SendApplicationFormToUserEmailWithResultAsync` →
`ResolveClientNameAsync` wraps its client-name read in `SideEffectGuard.RunAsync(...)` so a failed
lookup degrades to generic phrasing instead of aborting the order. Correct there, but "the runner
rethrows everything" is only true of what *reaches* it: anything the delegate swallows with
`SideEffectGuard` never reaches the `catch` and will not roll the transaction back.

---

## 8. Tests

**There are none.** `Test/Test/BackendAPI/BuildingBlocks.UnitTests/` holds exactly four files —
`CsvTextDecoderTests.cs`, `AesGcmSecretProtectorTests.cs`, `KeysetPageTests.cs`, `CursorCodecTests.cs`
— and a grep for `TransactionRunner`, `ITransactionScope` and `RunWithCompensation` across all of
`Test/` returns zero matches. Nothing pins the rethrow-untouched behaviour, the post-commit ordering
of `RunAsync<T>`, the per-compensation `try`, or "the original exception wins" — all four are the
reasons this class exists, and all four break silently under one careless edit: `catch (Exception ex)`
plus `throw ex;` still compiles, still rolls back, and still passes every test in the repository.
`ITransactionScope` is four methods, so a fake is about ten lines — close this gap before touching
the file.

---

## Sharp edges

- **No nesting guard (§6).** An inner `RunAsync` makes the inner rollback dispose the *outer*
  transaction, turns the outer rollback into a silent no-op, and answers a 500 carrying EF's
  `InvalidOperationException`.
- **`RunWithCompensationAsync` is not transactional (C4).** It opens and commits nothing; reaching
  for it because "it's on `TransactionRunner`" buys a cleanup handler and no atomicity.
- **`onCompensationFailed` is optional with no default logging (§3).** Omit it and a failed
  compensation vanishes without a trace, leaving the orphan it was meant to delete.
- **Non-database effects inside a `RunAsync` delegate survive the rollback (§7).** SMTP sends,
  `RemoveByTagAsync`, object-storage writes, OMS stored procedures — all outlive the transaction, and
  `RunAsync` offers no compensation hook.
- **The delegate's cancellation is its own business (§7).** The live call site drops `ct` three
  times, so a hung mail server holds a Postgres transaction open past request abort.
- **Repository methods self-flush (§5.1).** Inside a delegate that is harmless — they enlist. Called
  *before* `BeginTransactionAsync`, the same method commits alone and survives any later rollback.
  That is the original incident.
- **`CommitAsync`/`RollbackAsync` no-op silently when `_transaction` is null (§4).** Harmless in the
  normal path; it is what hides the double rollback in §6 step 3.
- **No tests (§8).** Every guarantee above is enforced only by the current text of the file.

---

## Wiring

| Piece | Where | Note |
|---|---|---|
| `ITransactionScope`, `TransactionRunner` | `BuildingBlocks/BuildingBlocks/Data/` | Static class. No DI registration, no state, no EF reference |
| `ATS.Data.UnitOfWork.IUnitOfWork` / `.UnitOfWork` | `ATS/Data/UnitOfWork/` | `: ITransactionScope`, empty body; `AddScoped` at `ATSServiceConfiguration.cs:99` |
| `PhilSys.Data.UnitOfWork.IUnitOfWork` / `.UnitOfWork` | `PhilSys/Data/UnitOfWork/` | **Not** `ITransactionScope`; `AddScoped` at `PhilSysServiceConfiguration.cs:57` |
| `ATSDBContext` | `ATSServiceConfiguration.cs:234` | `AddDbContext` = scoped, so the unit of work and every `IATSRepository` facet share one context and one connection |
| `IATSRepository` | `ATSServiceConfiguration.cs:47-48` | `ATSRepository` **decorated** by `ATSCacheRepository` — where the §7 cache side effect lives |
| `IOrderHistoryRepository` | `ATSServiceConfiguration.cs:58` | Forwards to the same `IATSRepository` instance, not a second context |
| `CustomExceptionHandler`, `SideEffectGuard` | `BuildingBlocks/Exceptions/Handler/` | The switch that gives bare `throw;` its value (`_ =>` is 500), and the suppress-and-log opposite — which also runs *inside* a runner delegate (§7) |

---

## Change X, also check Y

| If you change… | Also check… |
|---|---|
| The bare `catch` / `throw;` in either `RunAsync` | `CustomExceptionHandler`'s switch — the 404-vs-500 guarantee *is* that the type survives. Keep `throw;`, never `throw ex;` |
| `ITransactionScope`'s four members | Both `UnitOfWork` implementations, `PhilSys/.../IUnitOfWork.cs`, and the five hand-rolled services in §5.3 that call the members directly |
| `UnitOfWork.BeginTransactionAsync` | §6. A re-entrancy guard changes nested behaviour for every future call site; a null-check silently changes which transaction `Commit`/`Rollback` act on |
| `RunWithCompensationAsync`'s loop | `EndorsementSubmissionService.cs:269` — the only caller, and the only thing between a failed insert and a permanently orphaned blob |
| `RunAsync`'s parameter list | Both call sites. `RunAsync<T>` has none — changing it breaks nothing today and silently rots |
| `AddEmailInvitationRequestAsync` / `AddBulkUploadFileDetailsAsync` / `OrderHistory.AddAsync` | Their internal `SaveChangesAsync`. Removing it makes them tracker-only, which then *depends* on the runner's currently-no-op `SaveChangesAsync` (C6); keeping it means they must never run outside a transaction |
| `UpdateSingleEmailInvitationRequestStatusForSentEmailAsync` | It is `ExecuteUpdateAsync`, untracked. Rewriting it as a tracked update changes when the write reaches the server relative to the send |
| `ATSCacheRepository`'s `RemoveByTagAsync` calls | §7 — they fire on flush, not commit, so they run for rolled-back transactions |
| `SendApplicationFormToUserEmailAsync` | Its `throw new InternalServerException` on `!result.IsSent` is what makes a failed send roll the order back; its `CancellationToken.None` is what keeps the transaction open (§7) |
| Anything in `BuildingBlocks/Data/` | There are no tests (§8). Add them, or verify by hand that the exception type reaching `CustomExceptionHandler` is unchanged |
| A service you are migrating onto the runner | Design doc §6: does a repository method inside the block self-flush *before* the begin, and did the old `catch` wrap exceptions into a 500 callers now depend on |
