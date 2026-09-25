# ATS Notice Copy Lists — code explanation

The call chain, file by file, for adding and editing a row in `ats."EmailProcessDetails"`, and for
reading one back out on the send path. Read `ats-email-process.md` first for what the feature is
and why the shape is what it is.

---

## The files

| File | What it is |
|---|---|
| `Data/Entities/EmailProcessDetails.cs` | The row. `Id`, `EmailProcess`, `CCEmail`, `CreatedDate`, `IsActive` |
| `Data/EntityConfiguration/EmailProcessDetailsConfiguration.cs` | Table `ats."EmailProcessDetails"`, lengths, unique index on `EmailProcess` |
| `API/APIs/Migrations/ATS/20260925005309_AddEmailProcessDetailsATSMigration.cs` | Creates the table. **Lives in the API project, not the module** |
| `Data/DataSeed/ATSInitialData.cs` → `GetEmailProcesses()` | The five seeded rows |
| `Constants/AtsEmailProcess.cs` | The five legal process names, plus `All` |
| `Shared/EmailCopyList.cs` | Split / Normalize / Validate — the only thing that can police the list |
| `DTO/AddEmailProcessDTO.cs` | `EmailProcess`, `CCEmail`, `IsActive` — no `CreatedDate` |
| `DTO/EditEmailProcessDTO.cs` | `Id`, `CCEmail`, `IsActive` — **no `EmailProcess`** |
| `DTO/EmailProcessDetailsDTO.cs` | What both operations return |
| `Features/Web/EmailProcessManagement/Command/AddEmailProcess/` | Endpoint + handler + validator |
| `Features/Web/EmailProcessManagement/Command/EditEmailProcess/` | Endpoint + handler + validator |
| `Services/Settings/EmailProcessManagement/IEmailProcessManagementService.cs` | Both sides of the table: the console's writes, and `GetCopyListAsync` for the send paths |
| `Services/Settings/EmailProcessManagement/EmailProcessManagementService.cs` | Uniqueness guard, normalisation, the stamp — and the send path's read |
| `Data/Repository/EmailProcessManagement/ATSRepository.EmailProcesses.cs` | EF queries |
| `Data/Cache/EmailProcessManagement/ATSCacheRepository.EmailProcesses.Cache.cs` | The decorator |
| `Path/ATSPaths.cs` | The two gateway routes |

---

## Add — `POST /ats/addemailprocess`

### 1. Gateway

`ATSPaths.cs:198-207` declares `AddEmailProcess`: match `/ats/addemailprocess`, method `Post`,
cluster `GatewayConstants.OnePlatformApi`, transform `PathSet → /addemailprocess`. This typed
definition is what the gateway actually serves — the `ReverseProxy:Routes` block in
`appsettings.*.json` is dead configuration. Confirm with `GET /__routes`.

### 2. Endpoint — `AddEmailProcessEndpoint.cs`

A Carter module, discovered by assembly scan, no manual registration. `MapPost("addemailprocess")`
binds `AddEmailProcessRequest`, wraps it in the command, sends it, and returns
`Results.Ok(response.emailProcess)` — the DTO itself, not the wrapper record.

`.RequireAuthorization().RequireActiveAtsUser()` — authenticated *and* still an active ATS user.
Documented as `Produces<EmailProcessDetailsDTO>()` and `ProducesProblem(400)`.

### 3. Validation — `AddEmailProcessHandler.cs:7-51`

`ValidationBehavior<,>` runs this before the handler; a failure never reaches the service.

`RuleFor(x => x.emailProcess).NotNull()` first, then everything else inside
`When(x => x.emailProcess != null, ...)` so a null body produces one clear message instead of a
crash inside the rules that follow.

The `EmailProcess` check is **two separate `RuleFor` chains**, and that is load-bearing:

```csharp
RuleFor(x => x.emailProcess.EmailProcess)
    .NotEmpty().WithMessage("EmailProcess is required.");

RuleFor(x => x.emailProcess.EmailProcess)
    .Must(process => AtsEmailProcess.All.Contains(process.Trim(), StringComparer.Ordinal))
    .WithMessage($"EmailProcess must be one of: {string.Join(", ", AtsEmailProcess.All)}.")
    .When(x => !string.IsNullOrWhiteSpace(x.emailProcess.EmailProcess));
```

A trailing `.When()` in FluentValidation applies to **every rule in its chain**, not only the one
it follows. Written as a single chain, the `When` would switch the `NotEmpty` off for exactly the
empty value it exists to catch — an empty `EmailProcess` would pass validation and reach the
service. `EmailProcessValidationTests.AddValidator_ShouldRejectMissingProcess` covers both the
empty and the whitespace case.

The membership check is `StringComparer.Ordinal` — case matters, because the send path matches on
the exact string.

The copy-list rule delegates to `EmailCopyList.Validate`, which returns the message or `null`:

```csharp
RuleFor(x => x.emailProcess.CCEmail)
    .Must(copyList => EmailCopyList.Validate(copyList) is null)
    .WithMessage(command => EmailCopyList.Validate(command.emailProcess.CCEmail));
```

`Validate` is called twice — once to decide, once to phrase. It is pure string work over at most
a few dozen addresses, and the alternative is a rule per failure mode that reports only the first
one it happens to be ordered before.

Last, the empty-and-active rule: `IsActive` must `Equal(false)` when
`EmailCopyList.Split(CCEmail).Count == 0`.

### 4. `EmailCopyList.cs`

`Split` is `string.Split(',', TrimEntries | RemoveEmptyEntries)` — trimming even though `Normalize`
never writes spaces, because the column is hand-editable and a leading space makes a fragment
unparseable to MimeKit.

`Normalize` is `string.Join(',', Split(...))`. Null and whitespace both become `""`; the column is
NOT NULL, so a reader never has to treat null and empty as the same thing.

`Validate` walks the split list once: per-address length, then shape via
`BulkSubjectRowValidator.IsValidEmail` (reused, not restated — its own comment asks that the tiers
not drift), then a case-insensitive `HashSet` for duplicates. The total-length check comes last and
measures `string.Join(',', addresses)` — the **normalised** form, because that is what gets stored.
`EmailCopyListTests.Validate_ShouldMeasureTheNormalisedList_NotTheSubmittedOne` pins that: 25
addresses of 39 characters are 1023 as submitted with `", "` and 999 as stored, and the list is
accepted.

### 5. Handler → service — `EmailProcessManagementService.AddEmailProcessAsync`

Trims the process, then a check-then-write guard:

```csharp
if (await _emailProcessRepository.EmailProcessExistsAsync(emailProcess, cancellationToken))
    throw new BadRequestException($"A copy list for '{emailProcess}' already exists. Edit that one instead.");
```

The unique index is still the real guarantee — two simultaneous adds both pass this and the loser
dies on the constraint. The guard turns the *ordinary* case, an operator adding a process that is
already listed, into a message the screen can render instead of a 500.

The check is case-**insensitive** while the index is case-sensitive. That is intentional: the send
path's lookup is not case-sensitive, so `withdrawn` and `Withdrawn` would be the same list to it,
and letting both exist would make which one wins a matter of row order.

`CCEmail` is normalised here, `CreatedDate` is stamped `DateTime.UtcNow` by the server — the DTO
has no field for it — and `IsActive` comes from the caller. The saved entity is `.Adapt<>()`ed to
`EmailProcessDetailsDTO`.

No try/catch anywhere in the chain. `BadRequestException` is mapped by `CustomExceptionHandler`.
No `TransactionRunner` either — a single insert is already atomic.

### 6. Repository and cache

`ATSRepository.AddEmailProcessAsync` adds and calls `SaveChangesAsync`. Because it saves itself, it
would survive a rollback — if this ever joins a multi-write operation it has to sit inside a
`TransactionRunner` boundary.

`ATSCacheRepository.AddEmailProcessAsync` (Scrutor-decorated over the repository) calls through and
then `RemoveByTagAsync(CacheTags.EmailProcess)`.

### 7. Audit

`AtsAuditBehavior` picks the command up automatically, with no registration: the
`where TRequest : ICommand<TResponse>` constraint limits the behaviour to writes at compile time,
and `IsAtsCommand` — `Namespace.StartsWith("ATS.")` — limits it to this module.

`ResolveAction()` strips the `Command` suffix, giving `AddEmailProcess`. `ResolveArea()` takes the
namespace segment **immediately after `Features`**, which for `ATS.Features.Web.EmailProcessManagement.…`
is **`Web`**, not `EmailProcessManagement`.

That is worth knowing before reading the audit table and concluding something is wrong with this
slice. It is not specific to this feature: every slice under `Features/Web/` audits as `Web`,
including `UserManagement`. The method's own comment still describes the older
`ATS.Features.UserManagement.Command.AddUser` shape, from before the `Web`/`PublicApi` split put a
segment in front of the feature folder. Fixing it would re-label every existing `Web` audit row and
is out of scope here — but nothing about this feature should be changed to work around it.

Nothing here is in `AtsAuditRedactor.SensitivePropertyNames`, which is correct — a copy list of
team mailboxes is an operational setting, not a secret.

---

## Edit — `PATCH /ats/editemailprocess`

`ATSPaths.cs:209-218`. `MapPatch`, not `MapPut`: the row is only partly replaced — `EmailProcess`
and `CreatedDate` are not the caller's to set. Adds `ProducesProblem(404)` over Add's set.

`EditEmailProcessCommandValidator` requires `Id > 0` and then repeats Add's copy-list and
empty-and-active rules verbatim. Repeated rather than shared: the two commands address different
properties on different DTOs, and the shared part — what makes a list valid — already lives in
`EmailCopyList`.

`EditEmailProcessAsync` in the service:

1. `GetEmailProcessAsync(id)` — **tracked**, deliberately;
2. `null` → `NotFoundException($"Email process with ID {id} was not found.")`;
3. mutates `CCEmail` (normalised) and `IsActive`, and **nothing else**;
4. saves, adapts, returns.

`ATSRepository.EditEmailProcessAsync` calls `SaveChangesAsync` with no `Update()` call — the entity
arrived tracked from step 1, so the change tracker already has it.

The whole copy list is replaced, not merged. The screen edits it as one value, so a partial
payload would be indistinguishable from a deliberate removal.

---

## Read on the send path — `IEmailProcessManagementService.GetCopyListAsync`

The write side above is half the feature. This is the half that makes the rows matter.

### 1. The contract — `Services/Settings/EmailProcessManagement/IEmailProcessManagementService.cs`

```csharp
Task<IReadOnlyList<string>> GetCopyListAsync(
	string emailProcess,
	CancellationToken cancellationToken);
```

The same interface the console's Add and Edit handlers depend on — one service owning one table,
rather than a second reader sitting beside it. One method for all five notices rather than one per
notice: the interesting part is not the lookup, it is the failure behaviour, and that must not be
decided five ways.

**It is the one method on this interface that does not throw**, which is the thing to be careful
about when editing the file. `AddEmailProcessAsync` and `EditEmailProcessAsync` answer to an
operator watching a screen and raise `BadRequestException`/`NotFoundException`; this one answers to
a send path running behind a committed order with nobody watching, and must return something. The
send paths hold the interface, so they *can* see the writes — none of them call one, and the
`<remarks>` on the interface says why they must not.

### 2. The implementation — `EmailProcessManagementService.cs`

Four steps, each of which can end in an empty list:

```csharp
var emailProcesses = await SideEffectGuard.RunAsync(
	() => _emailProcessRepository.GetEmailProcessesAsync(cancellationToken),
	_logger,
	$"read the copy list for the {emailProcess} notice (it is sent to its recipient either way)",
	fallback: null,
	cancellationToken) ?? [];
```

1. **The read goes through `SideEffectGuard`.** A database that will not answer costs the copy, not
   the notice. The fallback is `null`, which `?? []` turns into the same empty list every other
   failure produces — so the three failure modes are indistinguishable to the caller, on purpose.
2. **The whole table, not one row.** `IEmailProcessRepository.GetEmailProcessesAsync` is the read
   the cache decorator holds under `emailprocess_v1_all`; a by-process query would miss it on every
   send. Five rows filtered in memory is cheaper than the query that avoids it.
   It goes to the **repository**, not to `GetEmailProcessesAsync` a few lines above it on the same
   service, even though that method wraps the identical call. That one is the console's: it lets the
   failure out so the screen can show it. Reusing it would hand the send path a throw, and the guard
   around this call is the whole point.
3. **`FirstOrDefault` with `OrdinalIgnoreCase`.** The unique index is case-sensitive, so a row
   hand-inserted as `withdrawn` is a different row to PostgreSQL and the same notice to this lookup.
   Matching loosely means such a row is used rather than silently ignored. No match → `LogWarning`
   (every value in `AtsEmailProcess.All` is seeded, so absence is a fault) → `[]`.
4. **`IsActive` honoured.** Inactive → `LogInformation` (an operator's decision, not a fault) → `[]`.
   Otherwise `EmailCopyList.Split(match.CCEmail)` — the same splitter the validator uses, so a
   hand-edited `"a@x.com, b@x.com"` loses its leading space here rather than throwing inside
   `MimeKit.MailboxAddress.Parse`.

It deliberately does **not** filter malformed addresses. See `ats-email-process.md` §5.

The `SideEffectGuard.RunAsync` call raises `CS8619` — `Task<List<T>>` passed where
`Func<Task<T?>>` is expected. `AtsNotificationService.cs:192` carries the identical warning for the
identical reason; it is the shape of the guard's signature, not a nullability bug here.

### 3. Registration — `ATSServiceConfiguration.cs`

```csharp
services.AddScoped<IEmailProcessManagementService, EmailProcessManagementService>();
```

One registration, not two — the send paths resolve the same service the Add and Edit handlers do.
Scoped, not singleton, despite the service holding no state: it depends on
`IEmailProcessRepository`, whose `DbContext` is per-request. The read is answered by the cache
decorator on that repository, so the lifetime costs nothing per notice.

### 4. The five call sites

| Send path | Process | Shape of the call |
|---|---|---|
| `WithdrawnEmailNotification` | `Withdrawn` | `new List<string>(await …)`, then the candidate's address appended if present |
| `DisputeEmailNotification` | `Dispute` | Resolved once, **outside** the retry loop — a second attempt re-sends, it does not re-read |
| `SubmittedFormEmailNotification` | `SubmittedForm` | `new List<string>(await …)`, then the requestor appended |
| `EndorsementSubmissionService` → `BuildCopyListAsync` | `ApplicationForm` **or** `FollowUp` | Chosen by the `isFollowUp` flag already in scope at the call site |

`BuildCopyListAsync` took an `emailProcess` parameter in this change; the requestor lookup below it
is unchanged. The invitation/reminder split is the point of storing a process per row — a single
literal could not distinguish them, and `isFollowUp` was already there to be read.

### 5. What was deleted

`Constants/ApplicationFormEmail.cs` is gone — it held only `CopyTeams`, so nothing survived.
`WithdrawnEmail.cs`, `DisputeEmail.cs` and `SubmittedFormEmail.cs` lost their `CopyTeam`/`CopyTeams`
and kept their `Subject`, which `ATSEmailService` still renders as the body `<h1>`
(`:563`, `:625`, `:685`).

---

## Cache layout

`ATSCacheRepository.EmailProcesses.Cache.cs`:

| Method | Cached? | Why |
|---|---|---|
| `GetEmailProcessesAsync` | Key `emailprocess_v1_all`, tag `CacheTags.EmailProcess` | Whole table, five rows, no parameters — unpaged on purpose. Also what every send reads through `GetCopyListAsync` |
| `GetEmailProcessAsync(id)` | **No** | Returns a tracked entity. A cached instance would be a detached object shared between requests, and the second edit would save the first one's changes |
| `EmailProcessExistsAsync` | **No** | A uniqueness guard reading a cached answer is not a guard |
| `AddEmailProcessAsync` / `EditEmailProcessAsync` | Invalidate | `RemoveByTagAsync(CacheTags.EmailProcess)` |

---

## Wiring

- `IATSRepository` extends `IEmailProcessRepository`, so both the repository and its cache
  decorator implement the new methods by being partial classes of the existing pair.
- `ATSServiceConfiguration.cs` registers
  `services.AddScoped<IEmailProcessRepository>(provider => provider.GetRequiredService<IATSRepository>())`
  — the narrow interface resolves to the same decorated instance, so the service gets the cached
  one — and `services.AddScoped<IEmailProcessManagementService, EmailProcessManagementService>()`,
  which is the single registration the console handlers **and** the four send paths resolve.
- `GlobalUsing.cs` carries `global using ATS.Services.Settings.EmailProcessManagement;`, which is
  what lets the notifications name the interface without a `using` of their own.
  **The Test project has no such global usings** — every test file that mocks the service needs the
  line written out.

---

## Tests

**`ATS.UnitTests/EmailCopyListTests.cs`** — `Split` (trims, drops blanks, empty for null/`","`),
`Normalize` (bare comma-separated; `""` when nobody is copied), and the normalised-length case.

**`ATS.UnitTests/EmailProcessValidationTests.cs`** — every known process accepted; `Withdrawal`,
`withdrawn` and `Application Form` rejected; empty and whitespace rejected with "EmailProcess is
required."; well-formed lists including hand-typed spacing accepted; malformed address, duplicate
(including case-only), over-long list and over-long single address each rejected with their own
message; empty-inactive accepted and empty-active rejected on **both** validators; `Id` required.

**`ATS.UnitTests/EmailProcessCopyListTests.cs`** — the read side, and almost all of it is the
unhappy path: the matching row's addresses returned; another process's list *not* returned; the
process matched case-insensitively (`withdrawn`, `WITHDRAWN`, `WithDrawn`); empty for a missing row,
an inactive row, an empty list and a repository that throws; a hand-edited row trimmed; a malformed
address deliberately **not** filtered; one repository read per resolve.

It constructs the real `EmailProcessManagementService` over a mocked `IEmailProcessRepository`, and
it stays a separate file from `EmailProcessManagementServiceIntegrationTests` even though both now
exercise the same class. They are not the same subject: one asserts that a bad write is rejected,
the other that a bad read is survived, and merging them would put two opposite expectations about
failure under one heading.

The logging is not asserted. Nothing else in this module verifies a log call, and pinning message
text would fail on a reword that changed no behaviour — what a caller depends on is the empty list.

**`ATS.UnitTests/EmailProcessSeedTests.cs`** — what the seed actually contains: one row per value in
`AtsEmailProcess.All` and no extras; `clientsupport` alone on the two order notices; both teams on
the three candidate notices; every address ending `@cibi.com.ph`; every list passing
`EmailCopyList.Validate`; `IsActive` true exactly where there are addresses.

This suite replaces a guarantee the cutover removed. While the lists were constants, the
notification suites asserted against them, so changing who CIBI copies broke a test. Those suites
now stub the resolver — correctly, since what they test is what a notice does with a list, not which
list — which left the agreed addresses pinned by nothing. The `@cibi.com.ph` assertion is the one
that matters most: the seed runs in **Production**, so a tester mailbox committed to
`GetEmailProcesses` reaches real candidate mail on the next deploy.

**The four notification suites** (`WithdrawnEmailNotificationTests`, `DisputeEmailNotificationTests`,
`SubmittedFormEmailNotificationTests`, `ApplicationFormEmailCopyTests`) construct their service with
a stubbed resolver from `Fixture/EmailCopyListFixture.cs`. It answers a named process with named
addresses and **everything else with an empty list** — that catch-all is what a send path reading
the wrong process looks like. `ApplicationFormEmailCopyTests` stubs `ApplicationForm` and `FollowUp`
with deliberately *different* addresses, so reading the invitation's row for a reminder fails.

**`ATS.IntegrationTests/EmailProcessManagementServiceIntegrationTests.cs`** — against Testcontainers
PostgreSQL through the real service and the cache decorator: Add persists and normalises, trims the
process, rejects a duplicate and a case-only duplicate with `BadRequestException`, accepts an empty
inactive list; Edit replaces wholesale, leaves `EmailProcess` and `CreatedDate` untouched, keeps the
addresses when deactivating, and throws `NotFoundException` for a missing id; the list read comes
back through the decorator ordered and including inactive rows.

**One thing to know before writing another test here.** `AppConfiguration.UseEnvironmentAsync`
returns early for the `Testing` environment *before* `IntializeDatabaseAsync`, so **the production
seed never runs in integration tests** — the five seeded rows do not exist. The seed is therefore
tested by calling `ATSInitialData.GetEmailProcesses()` directly, and `ats."EmailProcessDetails"` is
in `BaseIntegrationTest`'s TRUNCATE list (otherwise the second test to register "Withdrawn" dies on
the unique index). `BaseIntegrationTest` also evicts the `emailprocess` cache tag between tests.

### Running them

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~EmailProcess|FullyQualifiedName~EmailCopyList"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS.UnitTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS.IntegrationTests"
```

The ATS unit suite is fully green. It previously carried 8 failures in the notification suites,
which were the tester-mailbox swap in the email constants being reported rather than a defect;
deleting those constants removed the cause.
