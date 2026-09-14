# TransactionRunner

`BackendAPI/BuildingBlocks/BuildingBlocks/Data/`

The shared way to run a multi-write operation atomically, so no service hand-rolls
begin / save / commit / rollback again.

---

## 1. Why it exists

Every service that writes more than one row was repeating the same block:

```csharp
await _unitOfWork.BeginTransactionAsync(ct);
try
{
    // ... the actual work ...
    await _unitOfWork.SaveChangesAsync(ct);
    await _unitOfWork.CommitAsync(ct);
}
catch (Exception ex)
{
    await _unitOfWork.RollbackAsync(ct);
    _logger.LogError(ex, "...");
    throw new InternalServerException("...");
}
```

Seven lines of transaction plumbing wrapped around two lines of feature code, copied into
seven services. Three things go wrong with that:

1. **A copy forgets the rollback.** The transaction is never released.
2. **A copy re-wraps the exception.** `throw new InternalServerException(...)` around a
   `NotFoundException` turns a correct 404 into a 500 — `CustomExceptionHandler` can only
   map the type it actually receives.
3. **A copy swallows it.** `catch { return false; }` turns a failed write into a silent
   success.

The real incident that prompted this: `EndorsementSubmissionService` opened its transaction
*after* the order insert, and `AddEmailInvitationRequestAsync` calls `SaveChangesAsync`
itself. So the order committed immediately, the email was attempted afterwards, and a
failed send left a saved order whose candidate never received a link — invisible until
somebody noticed the form was never filled in.

---

## 2. The three methods

### `RunAsync(unitOfWork, work, ct)`

The common case. Begin → run the work → `SaveChanges` → commit. If `work` throws, roll
back and rethrow.

```csharp
await TransactionRunner.RunAsync(
    _unitOfWork,
    async () =>
    {
        await _atsRepository.AddEmailInvitationRequestAsync(order);
        await SendApplicationFormToUserEmailAsync(...);
        await _orderHistoryService.RecordAsync(...);
    },
    ct);
```

### `RunAsync<T>(unitOfWork, work, ct)`

Same, for work that returns a value. **The value is returned only after the commit
succeeds** — so you can never act on a result that was rolled back.

```csharp
var orderId = await TransactionRunner.RunAsync(
    _unitOfWork,
    async () =>
    {
        var order = await _repository.CreateAsync(dto);
        await _history.RecordAsync(order.Id);
        return order.Id;
    },
    ct);
```

### `RunWithCompensationAsync(work, compensate, onCompensationFailed)`

For work that has already touched something **a database transaction cannot undo** — an
uploaded blob, a record created in a third-party system. The compensation is the manual
rollback and runs only on failure.

```csharp
await TransactionRunner.RunWithCompensationAsync(
    work: () => _atsRepository.AddBulkUploadFileDetailsAsync(details),
    compensate: () => _objectStorageService.DeleteAsync(fileKey, ct),
    onCompensationFailed: ex => _logger.LogError(
        ex, "Failed to delete orphaned upload {FileKey}", fileKey));
```

There is also an overload taking **several** compensations:

```csharp
await TransactionRunner.RunWithCompensationAsync(
    work: () => _repository.SaveAsync(record),
    compensations:
    [
        () => _objectStorage.DeleteAsync(fileKey, ct),
        () => _remoteApi.CancelAsync(remoteId, ct)
    ],
    onCompensationFailed: ex => _logger.LogError(ex, "Cleanup failed"));
```

**Each compensation gets its own `try`**, so one that throws does not stop the ones after
it. That is the whole reason the overload exists — putting several `await`s inside a single
delegate shares one `try`, and the first failure silently skips the rest. They run in the
order given, so pass them **in reverse order of acquisition** (undo the last thing first)
when one depends on another.

---

## 3. The rules it enforces

**The `catch` inside `TransactionRunner` is not error handling.** It exists only to release
the transaction. The exception is rethrown **untouched** — not logged, not wrapped — so
`CustomExceptionHandler` still sees the real type and maps it to the right status code. A
`NotFoundException` thrown inside a transaction still produces a 404.

That is why your service should not add its own `catch` around the call. If you want to
log, log where you have the context; if you want a different status, throw a different
`BuildingBlocks.Exceptions` type from inside the work.

**A failing compensation never replaces the original error.** "Could not delete the blob"
is far less useful to the caller than the insert failure that caused it, so compensation
failures go to `onCompensationFailed` and the original exception continues to propagate.

---

## 4. Which helper to reach for

| Situation | Use |
|---|---|
| Should fail the request | Throw a `BuildingBlocks.Exceptions` type; `CustomExceptionHandler` answers |
| Multi-write that must be atomic | `TransactionRunner.RunAsync` |
| Touched storage / a third party the transaction cannot undo | `TransactionRunner.RunWithCompensationAsync` |
| Best-effort follow-up **after** the commit | `SideEffectGuard.RunAsync` |
| UI service calling the API | `ApiRequestExtensions.SendAsync<T>` |

`TransactionRunner` and `SideEffectGuard` are opposites and are easy to confuse:

|  | `TransactionRunner` | `SideEffectGuard` |
|---|---|---|
| On failure | rolls back, **rethrows** | logs, **suppresses** |
| Use for | the commit itself | work after the commit |
| Caller sees | the error | nothing |

Raising a notification after an application form is submitted is `SideEffectGuard` — the
submission is already durable, and a notification failure must not answer a successful
submission with a 500. Saving the form itself is `TransactionRunner`.

---

## 5. Wiring

`ITransactionScope` (same folder) declares the four members the runner drives. A module's
`IUnitOfWork` inherits it and adds nothing:

```csharp
public interface IUnitOfWork : ITransactionScope { }
```

This was done rather than moving every service onto a shared type, so nothing that injects
`IUnitOfWork` had to change.

**ATS is wired up. PhilSys is not yet** — `BackendAPI/Modules/PhilSys/Data/UnitOfWork/IUnitOfWork.cs`
still declares the four members itself. It has the same shape, so adopting it is a
one-line change whenever that module is next touched.

---

## 6. Migrating a service

These still hand-roll the block and can move over:

- `ATS/Services/ApplicationForm/ApplicationFormService.cs`
- `ATS/Services/Report/ReportService.cs`
- `ATS/Services/DisputeOrder/DisputeOrderService.cs`
- `ATS/Services/ApplicantSearchProjections/ApplicantSearchProjectionService.cs`
- `PhilSys/Services/UpdateFaceLivenessSessionService.cs`

Already migrated: `ATS/Services/EndorsementSubmission/EndorsementSubmissionService.cs`
(both `InsertEmailInvitationRequestAsync` and `ResendApplicationFormAsync`).

When migrating, check two things:

1. **Does a repository method inside the block call `SaveChangesAsync` itself?** Several
   do. Inside the transaction that is fine — the write enlists and rolls back with it. But
   if it runs *before* `BeginTransactionAsync`, it commits on its own and survives a later
   rollback. That is the bug this whole document exists because of.
2. **Was the old `catch` wrapping the exception?** If it threw
   `InternalServerException` over everything, moving to `TransactionRunner` will start
   surfacing the real types — which is the fix, but it does change the status codes that
   endpoint returns. Check nothing downstream depended on always getting a 500.

---

## 7. What not to break

- **Do not add a `catch` around a `TransactionRunner` call.** The rethrow is what preserves
  the status code.
- **Do not log inside the runner.** It has no context; log where you do.
- **Do not use it for best-effort work** — that is `SideEffectGuard`. Using the runner
  there means a failed notification fails the request.
- **Do not put a `SaveChangesAsync`-calling repository method outside the transaction** and
  assume the rollback covers it.
- **Keep compensations independent.** They must each be safe to run on their own, since one
  failing does not stop the others.
