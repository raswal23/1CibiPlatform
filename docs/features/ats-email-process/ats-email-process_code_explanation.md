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
| `DTO/EditEmailProcessDTO.cs` | `Id`, `EmailProcess`, `CCEmail`, `IsActive` — no `CreatedDate`, which is the row's |
| `DTO/EmailProcessDetailsDTO.cs` | What both operations return |
| `Features/Web/EmailProcessManagement/Command/AddEmailProcess/` | Endpoint + handler + validator |
| `Features/Web/EmailProcessManagement/Command/EditEmailProcess/` | Endpoint + handler + validator |
| `Features/Web/EmailProcessManagement/Query/GetEmailProcesses/` | Endpoint + handler — the console's list read |
| `Services/Settings/EmailProcessManagement/IEmailProcessManagementService.cs` | Both sides of the table: the console's writes, and `GetCopyListAsync` for the send paths |
| `Services/Settings/EmailProcessManagement/EmailProcessManagementService.cs` | Uniqueness guard, normalisation, the stamp — and the send path's read |
| `Data/Repository/EmailProcessManagement/ATSRepository.EmailProcesses.cs` | EF queries |
| `Data/Cache/EmailProcessManagement/ATSCacheRepository.EmailProcesses.Cache.cs` | The decorator |
| `Path/ATSPaths.cs` | The three gateway routes |
| `Constants/AtsModuleIds.cs` | `EmailManagement = 16` — the screen's permission, shared with the sender-account tab |

### Frontend

| File | What it is |
|---|---|
| `Component/ATS/EmailProcessManagement/EmailProcessManagement.razor`, `.razor.cs` | The board: table, search, Add button, row pencil |
| `Component/ATS/EmailProcessManagement/AddEmailProcessComponent.razor`, `.razor.cs` | The Add dialog |
| `Component/ATS/EmailProcessManagement/EditEmailProcessComponent.razor`, `.razor.cs` | The Edit dialog, including the rename warning |
| `Component/ATS/EmailProcessManagement/EmailCopyListInput.razor`, `.razor.cs`, `.razor.css` | The chip input both dialogs share |
| `SharedService/EmailCopyListDraft.cs` | The UI mirror of `EmailCopyList` — split, normalise, duplicate, caps |
| `ShareData/ATS/AtsEmailProcesses.cs` | The UI mirror of `AtsEmailProcess`, plus the display labels |
| `DTO/ATS/EmailProcessDetailsDTO.cs`, `AddEmailProcessDTO.cs`, `EditEmailProcessDTO.cs` | Transport models |
| `Services/ATS/EmailProcessManagement/IEmailProcessManagementService.cs` | The typed UI service contract |
| `Services/ATS/EmailProcessManagement/EmailProcessManagementService.cs` | The three HTTP calls |
| `Component/ATS/EmailManagement/EmailManagement.razor`, `.razor.cs` | The host page: **Email Accounts** and **Email Receiver** as two tabs, one permission (16) |
| `Component/ATS/EmailAccountManagement/EmailAccountManagement.razor` | The sibling tab — lost its `@page` when the two were grouped |
| `ShareData/ATS/ModuleList.cs` | Module 16's path, name and icon, and its restricted-administration entry |
| `Component/ATS/EmailProcessManagement/EmailProcessManagement.razor.css` | The board's own column widths, row alignment and chip clipping |
| `Component/ATS/EmailProcessManagement/AddEmailProcessComponent.razor.css` | The Add dialog's design, under the `epm-` prefix |
| `Component/ATS/EmailProcessManagement/EditEmailProcessComponent.razor.css` | Identical to the Add dialog's sheet — see *Styles* |
| `Component/ATS/EmailProcessManagement/EmailCopyListInput.razor.css` | The chip field, under the `ecl-` prefix |

---

## List — `GET /ats/getemailprocesses`

Added with the console screen; before that the two writes were reachable through the gateway with
nothing to render them.

`GetEmailProcessesEndpoint` is the plainest endpoint in the module — no query parameters, because
there is nothing to page or filter:

```csharp
app.MapGet("getemailprocesses", async (ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetEmailProcessesQuery(), cancellationToken);
    var response = new GetEmailProcessesResponse(result.emailProcesses);

    return Results.Ok(response);
})
```

It returns the **wrapper record**, not the list, so the JSON is `{"emailProcesses": [...]}` — the
same envelope shape as `GetEmailAccounts`. Both writes do the opposite and unwrap before
`Results.Ok`, returning the DTO bare. That asymmetry is why the UI service has a private
`GetEmailProcessesResponse` record and no equivalent on the other two calls.

`.RequireAuthorization().RequireActiveAtsUser()`, matching every other ATS Web endpoint. Note that
`GetEmailAccounts` beside it carries only `.RequireAuthorization()`; this one follows the module
-wide convention and its own sibling writes rather than the nearest neighbour.

The handler is a pass-through to `GetEmailProcessesAsync`, which is the **same method the send paths
read through** and the same cache key. That is deliberate: the console and the five notices cannot
disagree about what a list contains, because they are not two reads.

There is no handler validator — there is no input.

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
if (await _emailProcessRepository.EmailProcessExistsAsync(emailProcess, excludingId: 0, cancellationToken))
    throw new BadRequestException($"A copy list for '{emailProcess}' already exists. Edit that one instead.");
```

The unique index is still the real guarantee — two simultaneous adds both pass this and the loser
dies on the constraint. The guard turns the *ordinary* case, an operator adding a process that is
already listed, into a message the screen can render instead of a 500.

`excludingId: 0` excludes nothing. Edit passes the row's own id to the same method; see the Edit
section for why an add cannot.

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

`ATSPaths.cs`. `MapPatch`, not `MapPut`: the row is only partly replaced — `CreatedDate` is not the
caller's to set. Adds `ProducesProblem(404)` over Add's set.

`EditEmailProcessCommandValidator` requires `Id > 0`, then repeats Add's copy-list and
empty-and-active rules verbatim, and carries the same two `EmailProcess` chains — an unconditioned
`NotEmpty` beside a `Must(... All.Contains(..., Ordinal))` that only runs for non-whitespace values.
Repeated rather than shared: the two commands address different properties on different DTOs, and
the shared part — what makes a list valid — already lives in `EmailCopyList`. The two-chain shape is
load-bearing for the reason recorded in Add: a trailing `When()` applies to every rule in its chain,
so chaining the membership check onto the `NotEmpty` would switch off the `NotEmpty` for exactly the
empty value it exists to catch.

`EditEmailProcessAsync` in the service:

1. `GetEmailProcessAsync(id)` — **tracked**, deliberately;
2. `null` → `NotFoundException($"Email process with ID {id} was not found.")`. This runs *before*
   the rename guard, so a bad id is reported as a missing row and not as a collision;
3. rename guard, only when the submitted name differs from the stored one ordinally;
4. mutates `CCEmail` (normalised) and `IsActive`;
5. saves, adapts, returns.

```csharp
var emailProcess = emailProcessDTO.EmailProcess.Trim();

if (!string.Equals(existingEmailProcess.EmailProcess, emailProcess, StringComparison.Ordinal))
{
    if (await _emailProcessRepository.EmailProcessExistsAsync(
        emailProcess,
        excludingId: existingEmailProcess.Id,
        cancellationToken))
    {
        throw new BadRequestException(
            $"A copy list for '{emailProcess}' already exists. Edit that one instead.");
    }

    existingEmailProcess.EmailProcess = emailProcess;
}
```

Two things about that guard are easy to get wrong, and both are load-bearing:

**`excludingId` is not a tidy-up.** The name arrives in every edit payload, including one that
changes nothing else. Compared against the whole table, an ordinary save would collide with the row
it is holding and every edit would fail with "already exists".
`EditEmailProcessAsync_ShouldReplaceTheCopyListWholesale` is the test that catches it.

**Skipping the guard when the name is unchanged** keeps the common edit off an uncached query, and
means a pair of rows differing only by case — which the validator can no longer create, but a
hand-inserted row can — does not lock an operator out of fixing either one. A case-only change *is*
a rename ordinally and would still be guarded; it simply cannot reach the service, because
`AtsEmailProcess.All` is matched ordinally.

`ATSRepository.EditEmailProcessAsync` calls `SaveChangesAsync` with no `Update()` call — the entity
arrived tracked from step 1, so the change tracker already has it. That is also what makes a rename
a move rather than a delete-plus-add: the row keeps its `Id` and its `CreatedDate`.

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
| `EmailProcessExistsAsync` | **No** | A uniqueness guard reading a cached answer is not a guard. Takes an `excludingId`: Add passes 0, Edit passes the row it is holding |
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

### Frontend

- `UI/FrontendWebassembly/GlobalUsing.cs` carries
  `global using FrontendWebassembly.Services.ATS.EmailProcessManagement;`, and
  `ServiceConfig/FrontendServiceConfig.cs` registers
  `services.AddScoped<IEmailProcessManagementService, EmailProcessManagementService>()` beside the
  sender-account service.
- **The UI service and the backend service are two different types that share a name.** Both are
  `EmailProcessManagementService`, in `FrontendWebassembly.Services.ATS.…` and
  `ATS.Services.Settings.…` respectively. The WebAssembly project does not reference the ATS module,
  so nothing collides at build time — **but `Test.csproj` references both**, so a test that needs
  either one has to qualify it.
- The screen's permission is **module 16**, shared with the sender-account board it is tabbed
  beside. Declared twice: `AtsModuleIds.EmailManagement` on the backend, which the module seed
  reads, and `{ 16, ("emailmanagement", "Email Management", …) }` in `ShareData/ATS/ModuleList.cs`,
  which drives both the sidebar and `ATSLayout.CanAccessRoute`. Nothing checks the two agree at
  compile time. Because the path moved from `emailaccounts` to `emailmanagement`, the route resolves
  through `ModuleList` with no special case anywhere.
- 16 is in `RestrictedAdministrationModuleIds`, beside Audit Trail (15), so it is hidden from the
  User Management module picker unless the granter is a platform super admin or the ATS platform
  manager.
- **Module 17 existed for one uncommitted iteration and is retired, not free.** See the comment on
  `AtsModuleIds` and the cleanup SQL at the end of this document: any database that booted in the
  meantime has an orphan row the runtime seeder will not remove.

---

## The console screen

`Component/ATS/EmailProcessManagement/`. Follows `EmailAccountManagement`, the newest screen in the
console and the only other one whose page is split across `.razor` / `.razor.cs` / `.razor.css` —
Package, Client, Role and Module management all still hold their logic in an `@code` block. Its
sibling tab is that very screen, so the two are built the same way.

It is the **Email Receiver** tab of `Component/ATS/EmailManagement/EmailManagement.razor`, reached
from the sidebar under Manage → Email Management, at `/s&i/ats/emailmanagement`. The host carries
`RequireATSModule(16)` and both tabs sit behind that one id, so neither has a guard of its own —
unlike `Settings.razor`, whose three tabs are three separate permissions.

**Neither tab is a route.** Both `EmailProcessManagement` and `EmailAccountManagement` lost their
`@page` when the two were grouped: module 16's path belongs to the host, and a second URL for the
same tab would need its own branch in `CanAccessRoute` with no module behind it — the
ungrantable-entry trap the Notifications comment in `ModuleList` records. `/s&i/ats/emailaccounts`
therefore no longer resolves, and the one thing that linked to it, the
`EmailAccountsExhausted` notification, now points at the host. `EmailAccountManagement` keeps its
`RequireATSModule(16)` attribute: `SecurePageBase` reads attributes off the type whether or not the
component was routed to, so it still acts as a second check.

The host is UI-only plumbing. It owns no data, calls no service, and renders the two existing screens
as children — which is why grouping them changed nothing about how either one loads, validates or
authorizes. It defaults to the Accounts tab because that is the one the exhausted-senders
notification is about.

### One save, end to end

```
EditEmailProcessComponent.razor   <EmailCopyListInput @bind-Value="_edit.CCEmail" />
        ↓ Save, after MudForm.ValidateAsync and two guards the form cannot express
EditEmailProcessComponent.razor.cs   EditEmailProcessDialog.Close(DialogResult.Ok(_edit))
        ↓ the dialog closes with a DTO; the page owns the service call
EmailProcessManagement.razor.cs   EditEmailProcessAsync(row)
        ↓
Services/ATS/EmailProcessManagement/EmailProcessManagementService.cs
        ApiRequestExtensions.SendAsync<EmailProcessDetailsDTO>(
            () => _httpClient.PatchAsJsonAsync("ats/editemailprocess", new { editEmailProcess = dto }, ct), ct)
        ↓ named HttpClient "API"
YARP   RouteId EditEmailProcess, /ats/editemailprocess → PATCH /editemailprocess
        ↓
EditEmailProcessEndpoint → EditEmailProcessCommandValidator → EditEmailProcessHandler
        ↓
EmailProcessManagementService.EditEmailProcessAsync   rename guard, EmailCopyList.Normalize, save
        ↓
ATSCacheRepository   RemoveByTagAsync(CacheTags.EmailProcess)
        ↓ the snackbar and the reload happen on the way back
EmailProcessManagement.razor.cs   Snackbar.Add(...) then LoadEmailProcessesAsync()
```

The cache eviction at the end is what makes the reload meaningful: without it the table would come
straight back with the list it had before the save.

### Three things worth knowing before changing it

**The list read is not cursor-paginated.** `TableComponent` takes `Items="FilteredEmailProcesses"`
rather than `LoadServerData`, so `CrudPageBase.LoadCursorPagedDataAsync` and
`ExecuteAndReloadAsync` — which both end in `TableRef.ReloadServerData()` — do not apply here, and
the page has no `@ref` on the table. Search filters in the browser across the stored name, its
display label and the copy list.

**The dialogs return DTOs; the page posts them.** Same split as the sender-account board, and the
reason is that `CrudPageBase.OpenEditDialogAsync<TComponent, TDto>` uses one type parameter for both
the value passed in and the value returned. This dialog takes an `EmailProcessDetailsDTO` and
returns an `EditEmailProcessDTO`, so the helper does not fit and both dialogs are opened by hand
with a strongly-typed `DialogParameters<EditEmailProcessComponent>`. That is deliberate: the
string-named `parameterName` overload has nothing enforcing it at compile time, and
`{ component => component.Process, row }` does.

**The chip input commits on blur, not just on Enter.** An address typed without a keystroke to
commit it would otherwise be silently dropped by a click on Save. `Submit` also re-checks
`_copyListInput.HasError`, so a malformed chip cannot ride out in the payload — and the
empty-and-active rule is measured on the DTO rather than on the chip count, because the DTO is what
actually gets posted.

### Styles

**Everything this feature styles lives in scoped `.razor.css` files. `wwwroot/css/ats.css` is not
modified at all** — which is the point, and the reason is a bug that shipped.

| Sheet | Prefix | Covers |
|---|---|---|
| `EmailProcessManagement.razor.css` | `.ats-email-process-page` | Column widths, row alignment, chip clipping |
| `AddEmailProcessComponent.razor.css` | `epm-` | Dialog shell, sections, fields, toggle, footer |
| `EditEmailProcessComponent.razor.css` | `epm-` | Identical to the Add sheet |
| `EmailCopyListInput.razor.css` | `ecl-` | The chip field |

Shared classes are still used *as-is* — `.ats-management-page/-actions/-button/-card/-chips/-chip`,
`.ats-cell-lead`, `.ats-cell-muted`, `.ats-cell-action`, `.ats-status-pill`,
`.ats-status-board-intro`, and for the dialogs `.um-dialog`, `.ats-scroll-dialog`,
`.ats-dialog-shell/-header/-body/-footer` and the `.ats-dialog-headline` gradient header. None of
them are re-declared.

**Why not global.** The dialog family was first added to `ats.css` as `.ats-form-*`, on the
reasoning that a neutral shared name would let the fifteen existing scoped dialog copies (`.ee-`,
`.ap-`, `.ep-`, …) fold onto it later. `NewOrderComponent` had already been using
`.ats-form-section` for its own scoped sections. Its scoped rule sets only `margin-bottom: 26px`,
which still won on specificity — but the global rule also carried `border`, `border-radius`,
`background` and `overflow: hidden`, none of which the scoped sheet mentioned, so all seven of New
Order's sections grew a card border and a sunken background. The build stayed green and every test
passed.

Three lessons:

1. **A name that reads as generic is a claim that nobody else is using it.** That claim has to be
   checked with a repo-wide grep before the rule goes into a global sheet, not after.
2. **Specificity does not save you.** The existing scoped rule won the properties it declared and
   lost the ones it did not. A collision is silent exactly where the other screen is thinnest.
3. **A global sheet is shared by every screen whether it asks for it or not.** Scoped CSS is
   collision-proof by construction, which is worth more than saving one copy.

The cost is that the two dialog sheets are duplicates — scoped CSS cannot be shared between two
components, and Blazor gives each its own `b-…` attribute. They are byte-identical apart from the
first line of the header comment, and both say so; `fc AddEmailProcessComponent.razor.css
EditEmailProcessComponent.razor.css` should report only that line. Folding the console's sixteen
dialog designs onto one deliberately shared family is still worth doing — as its own change, with
its own name, and with the grep run first.

### Centring a MudSelect's value

Worth recording because it cost a round trip and the wrong answer looks right.

A `MudSelect` is **not** a text field. Its value renders in a `div` carrying `.mud-input-slot`,
nested inside `.mud-select-input` — where for a `MudTextField` the slot is the `<input>` itself.
The usual centring trick for these dialogs is `height: 42px` plus `line-height: 42px` on the slot,
which works on an `<input>` and leaves a select's text riding high in the box.

The fix is to flex-centre both levels, which is what `NewOrderComponent.razor.css` does for its
`.ats-select`:

```css
.epm-control ::deep .mud-select-input {
    display: flex;
    align-items: center;
    height: 42px !important;
    min-height: 42px !important;
    padding-top: 0 !important;
    padding-bottom: 0 !important;
}

.epm-control ::deep .mud-select-input > .mud-input-slot {
    display: flex;
    align-items: center;
    height: auto !important;
    min-height: 0 !important;
    line-height: normal;
}

/* MudBlazor still renders the adornment wrapper, and it inherits the input's height, so without
   this it stretches the flex row and pulls the value off centre again. */
.epm-control ::deep .mud-select-input > .mud-input-adornment {
    height: auto;
    max-height: none;
    margin-top: 0;
    align-self: center;
}
```

Note the third rule. Omitting it re-breaks the alignment in a way that looks like the first two did
not work — `ApplicationFormComponent.razor.css` carries a comment recording exactly that mistake.

Colour and font are not repeated on the slot: they inherit from `.epm-control ::deep .mud-input`.

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
inactive list; Edit replaces wholesale, leaves the name and `CreatedDate` alone when the name is
resubmitted unchanged, **moves** the list to another notice when it is renamed — same `Id`, same
`CreatedDate`, and the old name left with no row — refuses a rename onto a name another row holds,
keeps the addresses when deactivating, and throws `NotFoundException` for a missing id; the list
read comes back through the decorator ordered and including inactive rows.

`EditEmailProcessAsync_ShouldLeaveTheProcessAndCreatedDateUntouched` asserts something narrower than
its name once did. The name became editable, so what it now pins is that resubmitting the name you
were given does not disturb it — which is exactly the case the `excludingId` guard exists for.

**`Test/UI/EmailCopyListDraftTests.cs`** — the frontend mirror. `Split` trims and drops blanks and
returns nothing for null, whitespace, `","` and `",,"`; `Normalize` joins with a bare comma and
returns `""` for nobody; `Contains` matches case-insensitively; and `NormalizedLength` measures the
**stored** form, not the submitted one — the boundary case is 25 addresses of exactly 39 characters,
which are 999 stored and 1023 as submitted with `", "`, so the list is accepted and the same list
typed with spaces would not be. One character either side of 39 and the test would pass without
proving anything, which is why the length is asserted rather than assumed.

The caps themselves are asserted against 1000 / 255 / `','`. They are duplicated across two projects
on purpose — the WebAssembly frontend cannot reference the ATS module — so the assertion is the only
thing standing between a column resize on one side and a chip input that quietly disagrees on the
other.

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
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~Test.UI"
```

The ATS unit suite is fully green. It previously carried 8 failures in the notification suites,
which were the tester-mailbox swap in the email constants being reported rather than a defect;
deleting those constants removed the cause.

**`AtsHubGroupIsolationTests.Connection_WithNoIdentity_ShouldJoinNoGroupAndReceiveNothing` is flaky
under a full-suite run.** It passes on its own and passed a second full run of the same unchanged
binary; the first full run failed it. It is a SignalR connection racing container teardown and has
nothing to do with this feature — but it will show up as a red run occasionally, so re-run before
believing it.

---

## If you change this, also check that

| Change | Also check |
|---|---|
| Add a value to `AtsEmailProcess.All` | `GetEmailProcesses()` in the seed — a process with no entry still gets an empty inactive row, but it will copy nobody until one is added. Then `ShareData/ATS/AtsEmailProcesses.cs` **and its `Label`**, or the screen offers a name with no readable form |
| Rename a process constant | It is a data change, not a refactor: the stored string is what the five send paths match on. Existing rows keep the old value and stop being read |
| Resize the `CCEmail` column | `EmailCopyList.MaxLength` **and** `EmailCopyListDraft.MaxLength`, plus `EmailCopyListDraftTests.Caps_ShouldMatchTheBackendColumn` |
| Change what counts as a valid address | `BulkSubjectRowValidator.IsValidEmail` (backend) and `EmailValidationService` (UI). Both are deliberately the UI's and the backend's single definition; a third one is the drift the comments warn about |
| Add a field to `EditEmailProcessDTO` | The UI `EditEmailProcessDTO`, and whether the dialog should let an operator set it. `CreatedDate` is the one field that must stay off the wire |
| Change the wrapper property name on any of the three requests | The matching anonymous object in `Services/ATS/EmailProcessManagement/EmailProcessManagementService.cs`. Nothing checks these agree; a mismatch is a `400` with an empty body |
| Change the cache key or the eviction tag | `CacheTags.EmailProcess` and both write paths. A write that stops evicting leaves the console showing a list the notices are no longer using |
| Add an ATS module id | `AtsModuleIds` **and** `ShareData/ATS/ModuleList.cs`, and decide whether it belongs in `RestrictedAdministrationModuleIds`. 16 is Email Management; **17 is retired, not free** — see the cleanup SQL below |
| Add a tab to `Settings.razor` | The `RequireATSModule` list there **and** the `settings` branch of `ATSLayout.CanAccessRoute`, which carries its own literal `Overlaps([6, 7, 8])`. A tab in one and not the other is unreachable — navigation is refused before the page renders, so the tab never appears and nothing reports why. `EmailManagement.razor` needs no such branch: its path is module 16's, so the generic `ModuleList` lookup covers it |
| Move a module's path in `ModuleList` | Every hardcoded link to the old one. `BulkEmailNotificationProcessorService` builds the `EmailAccountsExhausted` notification's `LinkUrl` as a literal, and `NotificationCenter` / `NotificationsPage` resolve a link's last segment against `ModuleList` to decide whether the reader may see it — a link that no longer resolves is shown to everyone and then navigates nowhere |
| Restyle an Email Process dialog | `AddEmailProcessComponent.razor.css` **and** `EditEmailProcessComponent.razor.css` — they are deliberately identical, so change both or they drift. See *Styles* |
| Add a rule to `wwwroot/css/ats.css` | Grep all of `UI/FrontendWebassembly` for that class name first, and prefer a scoped `.razor.css`. `.ats-form-section` was already taken by `NewOrderComponent` the last time this feature put a neutral name in the global sheet |
| Change a dialog field to or from a `MudSelect` | The centring rules differ — see *Centring a MudSelect's value*. The `line-height` trick works on a text field and silently fails on a select |
| Add an `@onclick` inside an `@for` loop | Capture the loop variable in a local declared in the loop body first. A `for` variable is shared by every iteration, so the lambda sees its value when the click happens, not when the row rendered — for a remove button that is one-past-the-end, and a bounds guard turns it into a click that does nothing with no error anywhere. `foreach` does not have this problem. The chip remove button in `EmailCopyListInput.razor` carries the comment |

---

## Retiring module 17 — cleanup for databases that already booted

The copy-list screen was module 17 for one uncommitted iteration before it was folded into 16.
Nothing was deployed, but **any database that ran the API in the meantime has rows this change will
not clean up**, because `ATSDatabaseExtensions.SeedAsync` only *inserts* module ids the catalogue is
missing. It never updates and never deletes — deliberately, since Module Management lets an
administrator edit a module's name, and a seeder that overwrote names would clobber those edits.

Two things are left behind, and only the first actually breaks something:

**An orphan `ModuleDetails` row for 17.** The frontend catalogue no longer knows the id, so it
renders no sidebar entry for it and `CanAccessRoute` resolves nothing to it. Harmless on its own.
The problem is a `UserDetails` grant against it: whoever holds 17 has an accessible id that matches
no module, which is invisible rather than wrong — until somebody reuses 17 for a different feature
and quietly grants that feature to them. That is why `AtsModuleIds` records 17 as retired and not
free.

**Module 16's stored name is still "Email Accounts".** The sidebar reads its label from
`ModuleList`, so the console says "Email Management" either way; the stale name only shows in Module
Management, which lists the database rows.

```sql
-- Grants first: UserDetails.ModuleId is a foreign key to ModuleDetails with ON DELETE RESTRICT,
-- so deleting the module row while any grant points at it fails.
DELETE FROM ats."UserDetails"   WHERE "ModuleId" = 17;
DELETE FROM ats."ModuleDetails" WHERE "ModuleId" = 17;

-- Align the stored name with the merged module. Safe to skip if nothing reads it, but Module
-- Management does. UpdatedAt is timestamptz, so now() is right here: (now() at time zone 'utc')
-- yields a timestamp WITHOUT time zone, which PostgreSQL then re-interprets in the session
-- timezone on assignment and can shift by the local offset.
UPDATE ats."ModuleDetails"
SET "ModuleName"        = 'Email Management',
    "ModuleDescription" = 'Sender email account and notice copy list management module for ATS system.',
    "UpdatedAt"         = now()
WHERE "ModuleId" = 16;
```

Run it against a developer database before assuming the screen is broken: a stale grant is the one
symptom that looks like an authorization bug and is not. Check first with

```sql
SELECT "ModuleId", "ModuleName" FROM ats."ModuleDetails" WHERE "ModuleId" >= 15 ORDER BY 1;
SELECT "ModuleId", count(*) FROM ats."UserDetails" WHERE "ModuleId" = 17 GROUP BY 1;
```

A freshly created database needs none of this — it is seeded from the current catalogue and never
hears of 17.

Worth knowing when reading the result: `ModuleManagement` renders `ModuleName` from these rows, while
the sidebar renders its label from `ModuleList`. Until the `UPDATE` above runs, the two disagree and
the grant picker offers "Email Accounts" for the module the sidebar calls "Email Management".

