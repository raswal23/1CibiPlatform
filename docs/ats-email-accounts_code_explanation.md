# ATS Email Accounts — Code Explanation

Companion to `docs/ats-email-accounts.md`. That document explains *what* the feature does and
*why* the rules exist. This one exists so a developer can review the actual implementation
without opening every file cold — it walks the real call chains, file by file, naming the exact
method that calls the next one and what data crosses the boundary. Where a rule's reasoning
matters, it points back at the section of the high-level doc rather than repeating it.

Read this top to bottom once, then use it as a map: "I'm changing X, what else touches it" is
answered by finding X below and reading who calls it and what it calls.

---

## 1. Backend — one request end to end (Register)

Register is the most complete slice (validation → external SMTP side effect → DB write → OTP
row) so it is documented in full. Edit/Delete/Resend/Verify/GetAccounts follow the same shape
and are summarized as diffs against this one in §2.

```
POST ats/registeremailaccount
  → RegisterEmailAccountEndpoint.AddRoutes (Carter route)
    → sender.Send(RegisterEmailAccountCommand)                [MediatR]
      → RegisterEmailAccountCommandValidator                  [FluentValidation, runs first]
      → RegisterEmailAccountHandler.Handle
        → IAtsEmailAccountManagementService.RegisterAsync
          → IAtsEmailAccountRepository (existence/priority checks)
          → IOtpService.GenerateOtp()
          → IAtsEmailSender.SendWithCredentialsAsync           [live SMTP, throwaway session]
          → ISecretProtector.Protect (encrypt the password)
          → IAtsEmailAccountRepository.AddAsync                [INSERT AtsEmailAccount, Pending]
          → IAtsEmailAccountRepository.AddOtpAsync             [INSERT AtsEmailAccountOtp]
        ← RegisterEmailAccountResult(otpSent)
    ← RegisterEmailAccountResponse(otpSent)                    [200 OK]
```

### 1.1 Endpoint — `Features/Web/EmailAccountManagement/Command/RegisterEmailAccount/RegisterEmailAccountEndpoint.cs`

```csharp
app.MapPost("registeremailaccount", async (
    RegisterEmailAccountRequest request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var command = new RegisterEmailAccountCommand(request.emailAccount);
    var result = await sender.Send(command, cancellationToken);
    return Results.Ok(new RegisterEmailAccountResponse(result.otpSent));
})
```

- Carter binds the JSON body to `RegisterEmailAccountRequest(RegisterEmailAccountDTO emailAccount)`
  — the wrapper property name (`emailAccount`) is why the frontend service posts
  `new { emailAccount = account }` rather than the DTO bare (see §3.3).
- The only job of this file is to translate HTTP → MediatR command → HTTP. No business logic.
- Returns `200 OK`, not `201 Created` — the code comment explains why: the row exists but is
  `Pending`, so nothing usable was created yet from the caller's point of view.
- `.RequireAuthorization()` is the only access control at this layer; the page-level
  `RequirePermission`/`RequireATSModule` attributes (§3.1) are the frontend's gate, this is the
  backend's.

### 1.2 Command + Validator + Handler — `RegisterEmailAccountHandler.cs`

All three live in one file, which is the project convention for every Carter/MediatR slice.

- `RegisterEmailAccountCommand(RegisterEmailAccountDTO emailAccount) : ICommand<RegisterEmailAccountResult>`
  — the MediatR request type. `ICommand<T>` (not `IRequest<T>` directly) is a BuildingBlocks
  marker interface that lets `ValidationBehavior` and `LoggingBehavior` pipeline steps find it.
- `RegisterEmailAccountCommandValidator : AbstractValidator<RegisterEmailAccountCommand>` runs
  automatically before the handler, wired through `ValidationBehavior` in the MediatR pipeline
  (registered once for the whole app, not per-feature — see `BuildingBlocks` DI setup). If any
  rule fails, `RegisterEmailAccountHandler.Handle` is never called.
  - Field-by-field validation lives here, not in the entity or the DTO.
  - `Priority > 0` and `DailySendLimit` bounds are business rules explained in the doc comments
    inline (lower priority wins; upper daily-limit bound guards against a typo bypassing the
    pre-emptive quota check).
- `RegisterEmailAccountHandler.Handle` does exactly one thing: call
  `IAtsEmailAccountManagementService.RegisterAsync` and wrap the result. All actual logic is in
  the service, not the handler — the handler is DI boilerplate that MediatR needs to route the
  command.

### 1.3 Service — `Services/Settings/EmailAccountManagement/AtsEmailAccountManagementService.cs`

`RegisterAsync` (lines ~76-151) is the orchestrator. In call order:

1. `Normalize(account.EmailAddress)` — trims + lowercases so `Foo@X.com` and `foo@x.com` collide.
2. `_repository.EmailAddressExistsAsync(...)` — DB check, throws `ConflictException` if a dup.
3. `account.Priority ?? _repository.GetNextAvailablePriorityAsync(...)` — auto-assigns the next
   free priority if the caller left it blank.
4. `_repository.PriorityExistsAsync(...)` — throws `ConflictException` on collision.
5. `_otpService.GenerateOtp()` — from `BuildingBlocks`, same service Auth's registration uses.
6. `SendOtpThroughCredentialsAsync(...)` (private helper, §1.4) — **this runs before anything is
   written to the database.** If it throws, the DB has nothing to clean up.
7. Only after the send succeeds: build the `AtsEmailAccount` entity. Note
   `EncryptedPassword = _secretProtector.Protect(account.AppPassword, AtsEmailAccountSecrets.PasswordContext(emailAddress))`
   — the plaintext password from the request DTO never reaches the entity or the DB row.
   `AtsEmailAccountSecrets.PasswordContext(email)` (in `Services/EmailAccounts/AtsEmailAccountSecrets.cs`)
   builds the Data Protection purpose string, so a password protected under one email's context
   cannot be unprotected under another's context string.
8. `VerificationStatus = AtsEmailAccountStatus.Pending` — this is the actual mechanism that keeps
   an unproven account out of the sending rotation; see §4 for where `Pending` is read.
9. `_repository.AddAsync(entity, ...)` — `INSERT` into `ats."AtsEmailAccount"`.
10. `StoreOtpAsync(...)` (private helper) — `INSERT` into `ats."AtsEmailAccountOtp"`, hashed via
    `_hashService.Hash(otpCode)` (never the raw code).
11. Returns `BuildOtpSent(...)` → `EmailAccountOtpSentDTO`, which the frontend uses to open the
    OTP dialog (§3.2).

### 1.4 `SendOtpThroughCredentialsAsync` → `IAtsEmailSender.SendWithCredentialsAsync`

```csharp
var result = await _emailSender.SendWithCredentialsAsync(
    credentials, credentials.EmailAddress, "Verify your ATS sender email",
    ATSEmailService.AtsEmailAccountOtpBody(displayName, otpCode, _otpExpiryInMinutes),
    cancellationToken);

if (!result.IsSent) throw new BadRequestException(...);
```

`IAtsEmailSender` resolves to `ATSEmailService` (DI wiring in §5). `SendWithCredentialsAsync`
(`ATSEmailService.cs` lines ~189-222) is the one path in the whole feature that talks to SMTP
using credentials that are **not yet stored anywhere** — it builds a throwaway
`SmtpRateLimiter` + `SmtpConnectionPool` + `SmtpAccountContext`, uses them once via the shared
`SendOverContextAsync` (§1.5), and disposes all three (`using`/`await using`) regardless of
outcome. Nothing here touches `SmtpAccountPoolRegistry` — an unverified account has no place in
the registry yet.

### 1.5 `SendOverContextAsync` — the actual SMTP conversation

This private method in `ATSEmailService.cs` (lines ~233-367) is shared by every send path in the
feature: `SendWithCredentialsAsync` (register/edit/delete/resend OTP) and
`SendThroughAccountAsync` (real candidate invitations, §4) both funnel into it. One place decides
what an SMTP exception means, which is what `docs/ats-email-delivery.md` §6 calls out as an
invariant to preserve.

- Rate-limits first (`context.RateLimiter.WaitForSlotAsync`), then acquires a pooled connection
  (`context.Pool.AcquireAsync`) — throttle/connect failures are caught and returned as classified
  `EmailDeliveryResult`s before a message is ever built.
- `BuildMessage` sets `From` from `context.DisplayName`/`context.EmailAddress` — never from a
  configured constant — because the From header must match whatever mailbox actually
  authenticated the session, or the receiving server treats it as spoofed.
- Exception → outcome mapping happens via `SmtpFailureClassifier.ClassifySendFailure` for
  `SmtpCommandException` (the server answered with a status code), and by catch-block shape for
  `SmtpProtocolException` (session broken) / `IOException`, `SocketException`, `TimeoutException`
  (transport-level) — see the inline remarks for exactly why a lone transient must not switch
  accounts (risk of double-send) while a throttle or auth rejection must.

---

## 2. The other five slices, as diffs from Register

All five live under `Features/Web/EmailAccountManagement/`, one `{Endpoint,Handler}.cs` pair per
folder, same Carter+MediatR shape as §1.1/§1.2. Only what's different from Register is noted.

| Slice | Route | Handler calls | Distinct behavior |
|---|---|---|---|
| **EditEmailAccount** | `PATCH ats/editemailaccount` | `IAtsEmailAccountManagementService.EditAsync` | First calls `GetEditableAccountAsync`, which throws `ConflictException` if `_poolRegistry.IsLeased(accountId)` is true (§4.3). Computes `credentialsChanged` (email/password/host/port only — see `AtsEmailAccountManagementService.cs` lines 170-179); if false, updates and returns `null` immediately with **no OTP**. If true, the new values are serialized into `PendingEmailAccountChanges` and stored on the OTP row's `PendingChangesJson` column — **not** written to the account row — until verified. |
| **DeleteEmailAccount** | `POST ats/deleteemailaccount` (not `DELETE` — see `AtsEmailAccountService.cs` comment: "this sends a code, the row goes when the code comes back") | `IAtsEmailAccountManagementService.DeleteAsync` | Also gated by `GetEditableAccountAsync`'s lease check. Sends the OTP through the account's *existing* stored credentials (`UnprotectStoredPassword`), not new ones. The actual `DELETE` only happens in `ApplyVerifiedChangeAsync` after the code is confirmed (§2.1). |
| **VerifyEmailAccountOtp** | `POST ats/verifyemailaccountotp` | `IAtsEmailAccountManagementService.VerifyOtpAsync` | No SMTP send. Looks up the active OTP row via `_repository.GetActiveOtpAsync`, compares via `_hashService.Verify`, increments `AttemptCount` on failure (capping at `AtsEmailAccountOtpPolicy.MaxAttempts`), and on success calls `ApplyVerifiedChangeAsync` (§2.1). |
| **ResendEmailAccountOtp** | `POST ats/resendemailaccountotp` | `IAtsEmailAccountManagementService.ResendOtpAsync` | Reads the existing OTP row's `PendingChangesJson` (if any) so a resend during an in-flight edit doesn't lose the pending values; re-sends through whichever credentials (stored or pending) are actually being proven; calls `StoreOtpAsync` again, which invalidates the previous code first. |
| **GetEmailAccounts** | `GET ats/getemailaccounts` | `IAtsEmailAccountManagementService.GetAccountsAsync` | Pure read. `_repository.GetSnapshotsAsync` → `AtsEmailAccountSnapshot` list → `ToDto` projection (§2.2). No validator (a `IQuery<T>` with no input to validate). |

### 2.1 `ApplyVerifiedChangeAsync` — where a confirmed code actually takes effect

`AtsEmailAccountManagementService.cs` lines ~480-542. This is the single place that turns a
verified OTP into a real state change, called only from `VerifyOtpAsync`:

- `Purpose == Delete` → `_repository.DeleteAsync(entity)` then
  `_poolRegistry.InvalidateAsync(entity.AtsEmailAccountId)` — the registry entry (open SMTP
  sessions, limiter) is torn down in the same step as the DB row, so nothing keeps sending from a
  mailbox the operator just removed.
- `Purpose == Edit` (and `PendingChangesJson` is not null) → deserializes
  `PendingEmailAccountChanges` and copies every field onto `entity`, including
  `EncryptedPassword` (already encrypted when it was parked, per §2 table above — never
  re-encrypted here).
- Both `Register` and `Edit` then fall through to the same three lines:
  `entity.VerificationStatus = Verified`, `ConsecutiveFailureCount = 0`, `CoolingDownUntil = null`
  — a code just proved the credentials work, so any stale breaker state from before the change is
  discarded rather than left to expire on its own clock.
- `_poolRegistry.InvalidateAsync(...)` is called again at the end unconditionally — the registry
  may be holding a `SmtpAccountContext` built from the pre-edit credentials; this forces
  `GetContextAsync` (§4.2) to rebuild it from the row on the next send.

### 2.2 `ToDto` — what the frontend actually receives

`AtsEmailAccountManagementService.cs` lines ~599-623, called from `GetAccountsAsync`. Maps
`AtsEmailAccountSnapshot` (repository-shaped, includes `ConsumedInWindow`) to `EmailAccountDTO`
(wire-shaped). Two fields are **computed here, not stored**:
- `IsInUse = _poolRegistry.IsLeased(snapshot.AtsEmailAccountId)` — live registry state, not a DB
  column, so the "in use" badge in the table (§3) is only ever as stale as this one request.
- `IsSendable = snapshot.IsSendable(now)` — the selector's own eligibility rule (§4.1), evaluated
  here purely for *display*; the real gate the send path uses is
  `SmtpAccountPoolRegistry.GetNextSendableAccountAsync` re-evaluating the same rule independently
  against a fresh `now` (see §4.1's remark on why `now` is computed once per call, not shared
  across requests).
- `HasPassword = true` is a constant, not a read of `EncryptedPassword` — a DTO returning an
  account never carries the password in any form, by construction of this projection.

---

## 3. Frontend — the same round trip from the browser

### 3.1 Page shell — `Component/ATS/EmailAccountManagement/EmailAccountManagement.razor` (+ `.razor.cs`)

- Route/guards: `@page "/s&i/ats/emailaccounts"`, `@attribute [RequirePermission(6, 7)]`,
  `@attribute [RequireATSModule(16)]`, `@inherits CrudPageBase`. `OnInitializedAsync` checks
  `IsPageAuthorized` (set by those attributes via `CrudPageBase`) before calling
  `LoadAccountsAsync` — the comment in the code notes the guard is load-bearing: without it the
  attributes are inert (they don't block rendering by themselves).
- `LoadAccountsAsync` → `EmailAccountService.GetAccountsAsync()` → populates `_accounts`, which
  feeds `FilteredAccounts` (client-side search over `DisplayName`/`EmailAddress` — no server round
  trip per keystroke, deliberately, per the inline comment: the whole list is a handful of rows).
- Renders `<TableComponent<EmailAccountDTO> Title="Sender accounts" CountLabel="registered" ...>`
  — the `CountLabel` here is exactly what triggers `TableComponent.razor`'s extra
  `.table-component-heading` wrapper div around the title, which is why the CSS fix earlier in
  this feature's history had to widen a selector in `wwwroot/css/ats.css` to reach the title text
  (see git history / the CSS file's own comments around `.table-component-heading`).
- Edit/delete buttons use the shared `.ats-cell-action` class (not `.ats-management-edit-button`,
  which `PackageManagement.razor` uses) — both are styled from the shared `ats.css` sheet per the
  header comment in `EmailAccountManagement.razor.css`, which explicitly forbids re-declaring
  action-button styling in the page's own scoped CSS.

### 3.2 The two-step dialog sequence

`EmailAccountManagement.razor.cs` owns the sequencing, not the dialogs themselves:

```
AddEmailAccountAsync
  → DialogService.ShowAsync<AddEmailAccountComponent>()   → user fills form, dialog closes with RegisterEmailAccountDTO
  → EmailAccountService.RegisterAsync(account)            → POST ats/registeremailaccount
  → LoadAccountsAsync()                                    (row now visible as Pending)
  → ConfirmWithOtpAsync(response.Data)
      → DialogService.ShowAsync<VerifyEmailAccountOtpComponent>(OtpSent = response.Data)
      → user enters code, dialog closes with EmailAccountOtpResultDTO
      → returns true only if result.Data is { IsVerified: true }
  → LoadAccountsAsync() again if verified, else a warning snackbar (row stays Pending)
```

`EditEmailAccountAsync` and `DeleteEmailAccountAsync` follow the identical
service-call → `ConfirmWithOtpAsync` → reload pattern. The one branch difference: `EditAsync`'s
response `Data` can be `null` (no credential field changed → no OTP needed), which
`EditEmailAccountAsync` checks for and treats as an immediate success, skipping the OTP dialog
entirely.

Nothing is applied client-side on dialog close — every "success" the page shows is a reflection
of what the server already committed; the OTP dialog's own confirm button calls
`EmailAccountService.VerifyOtpAsync`, whose true effect happens in §2.1 on the server.

### 3.3 UI service — `Services/ATS/EmailAccountManagement/AtsEmailAccountService.cs`

One method per backend slice, all through `_httpClient` from `IHttpClientFactory.CreateClient("API")`
(the gateway-facing client, not a direct backend URL — see §5.2 for how `ats/...` resolves).
Each method wraps its payload in an anonymous object matching the endpoint's request record name
exactly — e.g. `new { emailAccount = account }` for Register (matches
`RegisterEmailAccountRequest.emailAccount` in §1.1), `new { editEmailAccount = account }` for
Edit, `new { deleteEmailAccount = request }` for Delete. Getting this wrapper property name wrong
is a silent 400/deserialization failure, not a compile error, because the backend request record
and the frontend anonymous object are two independently-written types with no shared contract.

Every method follows the same `try/catch` shape: non-success status → `ServiceResponse.Failure`
via `response.ReadErrorDetailAsync` (surfaces the backend's `BadRequestException`/
`ConflictException` message verbatim — this is how "the app password is wrong" reaches the
snackbar in §3.2); network/serialization exceptions → a generic "Unable to reach the server"
failure; `OperationCanceledException` is deliberately rethrown, not swallowed.

---

## 4. The sending path — how a real candidate invitation picks an account

This is the path §1-3 exist to feed: an account isn't useful until it's `Verified` and reachable
from here.

```
EmailNotificationProcessorService (unchanged orchestration, not detailed here)
  → IAtsEmailSender.SendATSEmailWithResultAsync(toEmail, subject, body, ct)   [ATSEmailService.cs]
      loop:
        → _poolRegistry.GetNextSendableAccountAsync(attemptedAccountIds, ct)  [4.1]
        → SendThroughAccountAsync(account.AtsEmailAccountId, ...)             [4.2]
            → _poolRegistry.GetContextAsync(accountId, ct)                    [4.2]
            → _poolRegistry.Lease(accountId)                                  [4.3]
            → SendOverContextAsync(context, ...)                              [§1.5, shared]
            → on success: _poolRegistry.ReportSuccessAsync(...)               [4.4]
            → on failure: _poolRegistry.ReportFailureAsync(...)               [4.4]
        loop continues if result.CanRetryOnAnotherAccount, else returns
```

### 4.1 `GetNextSendableAccountAsync` — `SmtpAccountPoolRegistry.cs` lines 62-83

Opens a **new** DI scope (`_scopeFactory.CreateScope()`) to resolve `IAtsEmailAccountRepository`
— required because the registry is a singleton and the repository/DbContext are scoped/
per-request. Calls `repository.GetSnapshotsAsync` (already ordered by `Priority` — see §4.5),
computes `now` once, then:

```csharp
return snapshots
    .Where(snapshot => !excludedAccountIds.Contains(snapshot.AtsEmailAccountId))
    .FirstOrDefault(snapshot => snapshot.IsSendable(now));
```

`excludedAccountIds` is the list the switcher loop (`SendATSEmailWithResultAsync`) has already
tried *for this one message* — passing `[]` on the first iteration means "nothing excluded yet,"
consistent with `IReadOnlyCollection<int>.Contains` on an empty collection always being false.
`IsSendable(now)` is defined on `AtsEmailAccountSnapshot` (§4.5) and is the single source of
truth for "can this account send right now" — active, verified, past cooldown, under quota.

### 4.2 `GetContextAsync` — the per-account cache — `SmtpAccountPoolRegistry.cs` lines 85-119

```csharp
if (_contexts.TryGetValue(accountId, out var existing)) return existing;

var gate = _contextLocks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
await gate.WaitAsync(cancellationToken);
try
{
    if (_contexts.TryGetValue(accountId, out existing)) return existing;  // re-check inside the lock
    var context = await BuildContextAsync(accountId, cancellationToken);
    _contexts[accountId] = context;
    return context;
}
finally { gate.Release(); }
```

Double-checked locking, one `SemaphoreSlim` per account id (not one global lock — two different
accounts can build their contexts concurrently). The first check is a fast lock-free path for the
overwhelmingly common case (context already built); the second check inside the lock exists
because two threads can both fail the first check and both reach `WaitAsync` before either has
built anything — without the re-check, the second thread through the gate would build and
discard a redundant `SmtpAccountContext`, wasting a real SMTP login (see `SmtpAccountContext.cs`'s
own remark: pool + limiter + identity are "only ever correct as a set," rebuilding one is not
free).

`_contexts` is a `ConcurrentDictionary<int, SmtpAccountContext>` — this **is** the per-account
"storage" for rate limiter/config raised earlier in this feature's design discussion: each
`SmtpAccountContext` (§ below) bundles one account's `SmtpConnectionPool` + `SmtpRateLimiter` +
identity, built once from the DB row and reused across every send through that account until
`InvalidateAsync` drops it (on delete, on edit, or on a `Permanent` account-scoped failure —
see §4.4).

`BuildContextAsync` (lines 121-182) is what actually constructs the triple: fetches the entity,
`_secretProtector.Unprotect`s the password (catching `CryptographicException` specifically —
almost always a rotated `SECURITY__SECRETPROTECTIONKEY`, surfaced as an actionable
`InvalidOperationException` rather than a raw crypto error), then `new SmtpRateLimiter(...)` and
`new SmtpConnectionPool(...)` — **one of each per account**, never shared, because the provider's
rate budget is per mailbox.

### 4.3 Leasing — `Lease`/`IsLeased` — `SmtpAccountPoolRegistry.cs` lines 322-357

`_leaseCounts` is a `ConcurrentDictionary<int, int>`, incremented in `Lease` and decremented by
the `IDisposable` `LeaseHandle` it returns. `SendThroughAccountAsync` holds the lease for the
entire send (`using var lease = _poolRegistry.Lease(accountId);`), so `IsLeased` reads true from
the moment a send starts until the `using` block disposes it. This is a **count**, not a flag,
because an account can carry several concurrent sends; a flag would be released by whichever
finishes first while others are still in flight. `GetEditableAccountAsync` in the management
service (§2) checks `IsLeased` before allowing an edit/delete to proceed, which is the actual
mechanism behind the "a send is in flight, try again" `ConflictException` surfaced to the UI as
`EditTooltip`/`DeleteTooltip` (§3.1).

### 4.4 `ReportSuccessAsync` / `ReportFailureAsync` — where the breaker state is written

`ReportSuccessAsync` opens a scope and calls `repository.RecordSuccessfulSendAsync`, which
(`AtsEmailAccountRepository.cs` lines 162-199) appends one row to `ats."AtsEmailSendLog"` **then**
`ExecuteUpdateAsync`s the account row to zero `ConsecutiveFailureCount` and clear
`LastFailureReason` — in that order, because if the process dies between the two writes the
account looks *more* consumed than it is, which is the safe direction to fail in (understating
consumption risks exceeding the provider's real cap).

`ReportFailureAsync` (lines 203-320) is the breaker logic itself:
- `!result.IsAccountFault` → returns `false` immediately, no DB write. This is the mechanism
  behind "a 550 does not count against the account" from the high-level doc — a recipient-shaped
  rejection never reaches this branch at all.
- `Throttled` → cools down for `_options.Value.ThrottleBackoffSeconds`, **and** if a context is
  already cached for this account, calls `throttledContext.RateLimiter.ReportThrottled(...)`
  directly on the in-memory object so the *current* pass stops using it before the next DB read
  would even notice.
- `Permanent` → sets `VerificationStatus = NeedsReverification` and calls
  `InvalidateAsync(accountId)` — same teardown as a delete/edit (§2.1), because a rejected
  credential means the cached pool/limiter are built from something the provider has disowned.
- Otherwise (`Transient`) → read-then-write of `ConsecutiveFailureCount`; trips (cools down) at
  `_options.Value.ConsecutiveFailureThreshold`, resets to 0 on trip rather than leaving it pinned
  at the threshold (explained inline: leaving it there would re-trip on the very next failure
  after cooldown instead of giving the account a fresh count).

### 4.5 `AtsEmailAccountSnapshot` and `BuildSnapshotQuery` — the quota window

`AtsEmailAccountRepository.cs` lines 52-86. `windowStart = DateTime.UtcNow - QuotaWindow`
(`QuotaWindow` defaults to `AtsEmailDeliveryOptions.QuotaWindowHours`, floored at 1 hour) is
computed **once**, outside the EF query, then used inside a correlated subquery:

```csharp
_dbContext.EmailSendLog
    .Where(log => log.AtsEmailAccountId == account.AtsEmailAccountId && log.SentAt >= windowStart)
    .Sum(log => (int?)log.RecipientCount) ?? 0
```

This is `ConsumedInWindow` on `AtsEmailAccountSnapshot`, which the snapshot's own `IsSendable(now)`
method (referenced throughout §4.1/§2.2) compares against `DailySendLimit` to derive
`RemainingInWindow` and the overall sendable boolean, alongside the `IsActive`/`VerificationStatus`/
`CoolingDownUntil` checks. One query, no N+1 — the inline remark explains this is deliberate
because the selector calls this on the hot send path and N+1 there would put N DB round trips on
the critical path of every message.

---

## 5. Wiring — DI and routing (how the pieces above find each other)

### 5.1 `BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs`

```csharp
services.AddScoped<IAtsEmailAccountManagementService, AtsEmailAccountManagementService>();  // line 107
services.AddSingleton<ISmtpAccountPoolRegistry, SmtpAccountPoolRegistry>();                  // line 132
services.AddScoped<IAtsEmailAccountRepository, AtsEmailAccountRepository>();                 // line 137
services.AddKeyedScoped<IEmailService, ATSEmailService>("ats");                               // line 148
services.AddScoped<IAtsEmailSender>(provider =>
    (IAtsEmailSender)provider.GetRequiredKeyedService<IEmailService>("ats"));                 // lines 155-156
```

`ATSEmailService` is registered once, as a **keyed** `IEmailService` (key `"ats"`, distinguishing
it from whatever other module's `IEmailService` might be registered), and then re-exposed as the
plain, unkeyed `IAtsEmailSender` by resolving the same keyed instance and casting — the comment
at line 151-154 explains why: the OTP-sending code in the management service needs the
credentials-based overload (`SendWithCredentialsAsync`, §1.4) that only `IAtsEmailSender`
declares, but it shouldn't have to know about the `"ats"` key. Both interfaces are implemented by
the one `ATSEmailService` class.

`ISmtpAccountPoolRegistry` is the **only singleton** in this list — everything else (management
service, repository) is scoped per-request, which is exactly why the registry can't just take a
constructor-injected `ATSDBContext` and instead threads `IServiceScopeFactory` through every
method that touches the database (§4.1, §4.2, §4.4).

### 5.2 Gateway routes — `BackendAPI/Modules/ATS/Path/ATSPaths.cs`

Six `PathSet` route entries (`GetEmailAccounts`, `RegisterEmailAccount`, `VerifyEmailAccountOtp`,
`ResendEmailAccountOtp`, `EditEmailAccount`, `DeleteEmailAccount`), each mapping
`/ats/<verb>` → `/<verb>` on the backend. This is the layer that lets the frontend's `"API"`
`HttpClient` (§3.3) call `ats/registeremailaccount` and have YARP forward it to the Carter
endpoint in §1.1 without the frontend knowing the backend's real base path. `PathSet` (rather
than a route with parameters) is used because none of these six routes take a path segment — all
identifiers travel in the body.

---

## 6. Quick reference — "I'm changing X, what do I also need to check"

| If you touch... | Also check |
|---|---|
| `AtsEmailAccountManagementService.RegisterAsync`/`EditAsync` validation | `RegisterEmailAccountCommandValidator` (backend) and any client-side `MudForm` rules in `AddEmailAccountComponent`/`EditEmailAccountComponent` (frontend) — they are two independent copies of the same rules, not shared code. |
| `EmailAccountDTO` shape | `AtsEmailAccountManagementService.ToDto` (§2.2, the only place that builds it) and `EmailAccountManagement.razor`'s table columns that read the new/changed field. |
| Breaker thresholds/cooldowns | `AtsEmailDeliveryOptions` (config-bound, §1's `_options`) and the `AtsEmailAccountBreakerTests` fixture, which asserts the exact thresholds. |
| `SmtpAccountPoolRegistry`'s cached context | Every call site of `InvalidateAsync` (§2.1 delete/edit, §4.4 permanent failure) — a new cache-invalidation path must be added in all three kinds of "this account's credentials or existence just changed" events, not just one. |
| The OTP body/copy | `ATSEmailService.AtsEmailAccountOtpBody` (§1.4) is the only place it's generated; the frontend's `VerifyEmailAccountOtpComponent` only renders the six-box input, it doesn't know the copy. |
| Gateway route names | `ATSPaths.cs` (§5.2) **and** `AtsEmailAccountService.cs`'s literal route strings (§3.3) **and** the Carter endpoint's `MapPost`/`MapGet` path (§1.1) — three independent strings that must agree, with nothing enforcing that at compile time. `GET /__routes` is the way to verify they actually match at runtime. |

---

## When to update this document

Whenever a slice's call chain changes shape — a new step is inserted, a call moves to a
different file, or a DI registration changes — update the relevant section above. If the change
is additive (a new slice, following the existing pattern exactly), add one row to the §2 table
rather than a new full walkthrough. This document is allowed to go stale on *prose explanations*
of why something is true (that drifts slower); it should not go stale on *which file calls
which* — that's the whole point of it existing.
