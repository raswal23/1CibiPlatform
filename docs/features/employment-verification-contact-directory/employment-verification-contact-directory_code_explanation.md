# Employment Verification contact directory — code walkthrough

Companion to
[employment-verification-contact-directory.md](employment-verification-contact-directory.md).
That document says what was built and why; this one is for someone about to change the
code and needs to know what calls what first.

## One request, end to end: loading a page of contacts

### Frontend

**1. `UI/FrontendWebassembly/Pages/EmploymentVerification/Contacts.razor`**

```razor
@page "/employmentverification/contacts"
@layout EVLayout
@inherits CrudPageBase
@attribute [RequirePermission(8, 9)]
@inject IContactDirectoryService ContactDirectoryService
```

`TableComponent` is handed `LoadServerData="LoadContactsAsync"` and
`CursorPagerState="_contactsLoader"`. The second parameter is what swaps `MudTablePager`
for `CursorTablePager` (First / Prev / Next, no jump-to-page — a keyset cursor cannot
jump).

**2. `Contacts.razor.cs` → `LoadContactsAsync`**

```csharp
private Task<TableData<EmploymentVerificationContactDTO>> LoadContactsAsync(
    TableState state,
    CancellationToken cancellationToken) =>
    LoadCursorPagedDataAsync(
        _contactsLoader,
        state,
        _searchString,
        (cursor, pageSize) => ContactDirectoryService.GetContactsAsync(
            cursor, pageSize, _searchString, cancellationToken));
```

The third argument is the **signature**. `CursorTableLoader.LoadAsync` resets its cursor
stack only when that string changes, so every filter that invalidates the keyset walk has
to appear in it. Only the search term does today.

**3. `Services/EmploymentVerification/Implementation/ContactDirectoryService.cs`**

Builds the query string against the **public gateway path**, never the internal Carter
route, and projects the envelope away:

```csharp
private const string GetContactsPath = "employmentverification/getcontacts";
...
return ApiRequestExtensions.SendAsync<
    GetContactsResponseDTO,
    KeysetPaginatedResult<EmploymentVerificationContactDTO>>(
    () => _httpClient.GetAsync(query, cancellationToken),
    response => response.Contacts,
    cancellationToken);
```

`ApiRequestExtensions` owns the send / `IsSuccessStatusCode` / `ReadErrorDetailAsync`
sequence, and — importantly — rethrows `OperationCanceledException` rather than reporting
it as a failure, so a `MudTable` reload cancellation is not surfaced as an error toast.

Registered in `ServiceConfig/FrontendServiceConfig.cs` beside the existing
`IEmploymentVerificationService`.

### Gateway

**4. `BackendAPI/Modules/EmploymentVerification/Path/EmploymentVerificationPaths.cs`**

```csharp
new RouteDefinitionDTO(
    RouteId: "GetEmploymentVerificationContacts",
    MatchPath: "/employmentverification/getcontacts",
    ClusterId: GatewayConstants.OnePlatformApi,
    Methods: [GatewayConstants.HttpMethod.Get],
    Transforms: new Dictionary<string, string>
    {
        ["PathSet"] = "/api/employment-verification/contacts"
    }),
```

`PathSet` rather than `PathPattern` because these three routes have no `{parameter}` to
substitute. The GET, POST and PATCH entries all target the **same** backend path and are
distinguished only by method, so they must stay three separate entries — one entry
listing three methods would forward a PATCH to the GET handler.

Nothing goes in the gateway `appsettings` files; that `ReverseProxy:Routes` section is
dead configuration with no reader.

### Backend

**5. `Features/ContactDirectory/Query/GetContacts/GetContactsEndpoint.cs`**

`[AsParameters]` binds the query string; `ISender.Send` dispatches; the result is wrapped
in `GetContactsEndpointResponse`. Carries the full metadata set
(`.WithName`/`.Produces`/`.ProducesProblem`/`.WithSummary`/`.WithDescription`/`.RequireAuthorization`)
modelled on `GetSentRequestsEndpoint` — the one pre-existing EV endpoint that is
guide-compliant, rather than the three thin ones beside it.

**6. `GetContactsHandler.cs`** — validator then handler in one file, as the guide requires.
`GetContactsQueryValidator` bounds `PageSize` to `KeysetPage.MaxPageSize`; the handler
builds a `KeysetPaginationRequest` and delegates.

**7. `Services/ContactDirectory/ContactDirectoryService.cs` → `GetContactsAsync`**

This is where the cursor work happens — the repository stays a pure query:

```csharp
var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);
var afterId = Guid.TryParse(fields?[1], out var parsedId) ? parsedId : (Guid?)null;
var afterCompanyName = afterId.HasValue ? fields![0] : null;
var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

var rows = await contactRepository.GetContactsPageAsync(
    paginationRequest.SearchTerm, afterCompanyName, afterId, pageSize + 1, cancellationToken);

var (items, hasMore) = KeysetPage.Trim(rows, pageSize);

var nextCursor = hasMore
    ? CursorCodec.Encode(items[^1].CompanyName, items[^1].Id.ToString())
    : null;

long? totalCount = afterCompanyName is null
    ? await contactRepository.CountContactsAsync(paginationRequest.SearchTerm, cancellationToken)
    : null;
```

`pageSize + 1` is deliberate: the extra row only signals that a next page exists and
`KeysetPage.Trim` removes it. The count is fetched **only on the first page** — counting
on every page would cost a full scan per click, and the frontend loader reuses the value
captured on page one for the whole walk.

**8. `Data/Repository/ContactDirectory/ContactDirectoryRepository.cs`**

```csharp
if (afterCompanyName is not null && afterId.HasValue)
{
    // Hoisted out of the lambda: closing over afterId.Value hands EF a
    // Nullable<Guid>.Value node to translate.
    var seekId = afterId.Value;

    query = query.Where(contact =>
        string.Compare(contact.CompanyName, afterCompanyName) > 0
        || (contact.CompanyName == afterCompanyName
            && contact.Id.CompareTo(seekId) > 0));
}

return await query
    .OrderBy(contact => contact.CompanyName)
    .ThenBy(contact => contact.Id)
    .Take(take)
    ...
```

The OR-expansion, matching `ATSRepository.Users.cs` and `ATSRepository.UserClients.cs` —
the repo's existing composite-keyset shape. A PostgreSQL row-value comparison
(`EF.Functions.GreaterThan` over `ValueTuple`) would produce a single index seek instead
of two range scans, but the OR form is what the codebase already proves out, and at this
scale the difference is not worth being the first caller of a different translation.

**The `ORDER BY` must stay in step with `IX_EmploymentVerificationContacts_CompanyName_Id`.**
Change one and the other stops being a seek.

Search spans both columns with `EF.Functions.ILike`, because someone looking for a contact
knows either the company or the mailbox and rarely which one the row was filed under.

**9. `Data/Cache/ContactDirectory/ContactDirectoryCacheRepository.cs`**

Applied by Scrutor, so it wraps the repository transparently:

```csharp
if (afterCompanyName is not null)
{
    return repository.GetContactsPageAsync(
        searchTerm, afterCompanyName, afterId, take, cancellationToken);
}

var key = $"evcontact_first_take_{take}_search_{searchTerm}";
```

Only the **first page** is cached; cursor pages are high-cardinality and pass straight
through. `take` is in the key because page size 10 and page size 50 are different first
pages. The count shares the `employmentverification:contacts` tag so one
`RemoveByTagAsync` clears both.

`ContactExistsAsync` and `GetContactAsync` deliberately pass through — the first is the
duplicate guard and must never read a cached answer, the second returns an entity the
service mutates.

## The write path — and where the 409 comes from

`AddContactCommand` → `AddContactHandler` → `ContactDirectoryService.AddContactAsync`:

```csharp
var companyName = NormalizeCompanyName(contactDTO.CompanyName);      // Trim()
var emailAddress = NormalizeEmailAddress(contactDTO.EmailAddress);   // Trim().ToLowerInvariant()

await GuardAgainstDuplicateAsync(companyName, emailAddress, null, cancellationToken);
```

The lower-casing is load-bearing: the unique index is a plain composite index on the
stored values — there is no `citext` column and no functional index — so case folding has
to happen before the row is written, or `HR@x.com` and `hr@x.com` both fit.

`GuardAgainstDuplicateAsync` throws `ConflictException`, which `CustomExceptionHandler`
maps to a 409 `ProblemDetails`. It is check-then-write and can lose a race; the unique
index is the real authority and the losing side of a genuine race gets a 500, which is
correct and rare. **There is no try/catch on `DbUpdateException`** — the guide forbids it,
and catching it here would convert a 409 into something less specific.

`EditContactAsync` passes `contactDTO.Id` as `excludeId` so a contact being renamed does
not collide with itself, and notes in a comment that there is no "in use" deactivation
guard (unlike ATS modules and clients) because nothing references a contact row yet.

## Wiring that is invisible from any single file

| Thing | Where | Note |
|---|---|---|
| Repository + decorator + service | `ServiceConfig/EmploymentVerificationServiceConfiguration.cs` → `AddEmploymentVerificationServices` | A **second** Scrutor pair beside the requests one; handlers, validators and Carter endpoints are assembly-discovered and need no registration |
| `DbSet<EmploymentVerificationContact>` | `Data/Context/EmploymentVerificationDbContext.cs` | The configuration is found by `ApplyConfigurationsFromAssembly`; no `OnModelCreating` edit |
| Module namespaces | `BackendAPI/Modules/EmploymentVerification/GlobalUsing.cs` | Added `BuildingBlocks.Exceptions`, `BuildingBlocks.Pagination` and the four `ContactDirectory` namespaces |
| Frontend namespaces | `UI/FrontendWebassembly/GlobalUsing.cs` | Added the EV DTO, service-interface and routes namespaces |
| Seed script copied to output | `BackendAPI/API/APIs/APIs.csproj` | `<Content Include="Migrations\**\*.sql" CopyToOutputDirectory="PreserveNewest" />`. **`Content`, not `None`** — `Content` flows to referencing projects, so `Test.csproj` gets the script and integration tests migrate the same seeded schema production does |
| Route strings | `UI/FrontendWebassembly/ShareData/EmploymentVerification/EmploymentVerificationRoutes.cs` | The `@page` directive, the `EVLayout` tab and the legacy redirect must agree, and nothing enforces that at compile time — hence one home for the constants |
| Stylesheet load order | `UI/FrontendWebassembly/wwwroot/index.html` | `ev.css` must load **after** `ats.css`: it re-points the `--management-*` properties the `.ats-management-*` rules read |

## Why `ev.css` is global rather than scoped

Three reasons, and they are the same reasons the guide gives:

1. The dialog markup is split across **three** components — `AddContactComponent`,
   `EditContactComponent` and the `ContactDialogFields` they share. A scoped sheet
   carries one component's scope attribute and would not match the others.
2. The MudBlazor input overrides would need `::deep` in scoped CSS, which the guide calls
   out as fragile to duplicate.
3. The page head, stat strip and drawer appear on more than one page.

The accent re-pointing is the whole trick:

```css
.ev-management-page {
    --management-blue-600: var(--c-ev-accent);
    --management-blue-500: var(--c-ev-accent-soft);
    --management-blue-tint: var(--c-ev-tint);
    --management-blue-tint-2: var(--c-ev-tint-2);
    /* neutrals deliberately left alone - borders and body text should match ATS */
}
```

Every shared `.ats-management-*` rule then renders teal inside EV with **no rule copied**.

## The migration

`BackendAPI/API/APIs/Migrations/EmploymentVerification/20260923031538_AddEmploymentVerificationContacts.cs`
was **scaffolded**, not hand-written, then its `Up()` body was extended with one call.
Scaffolding is what sidesteps the guide §14 trap: EF generates the `.Designer.cs`
carrying `[DbContext(...)]` and `[Migration(...)]`, without which `MigrateAsync()` skips
the migration silently and integration tests fail with `42703: column "X" does not exist`
while the build stays green.

```csharp
migrationBuilder.Sql(ReadSeedScript());
```

`ReadSeedScript()` reads from `AppContext.BaseDirectory`, the same way
`ATSDatabaseExtensions` loads `Scripts/quartz_postgres.sql`, and throws if the file is
absent — a mis-set `<Content>` item would otherwise make the migration a silent no-op and
ship an empty table that looks correctly migrated.

`IsActive` carries **no** `HasDefaultValue(true)`. A database default without a matching
`.ValueGeneratedOnAdd()` makes the snapshot disagree with the model and trips
`PendingModelChangesWarning`; the C# property initialiser on the entity is the default
instead.

## Change X, also check Y

| If you change… | Also check |
|---|---|
| The `ORDER BY` in `GetContactsPageAsync` | `IX_EmploymentVerificationContacts_CompanyName_Id`, and the cursor fields `ContactDirectoryService` encodes |
| The cursor field count | `CursorCodec.Decode(cursor, 2)` in the service, **and** the `|` rule in both validators |
| Anything cached | Both keys in `ContactDirectoryCacheRepository` — the page and the count share a tag; `take` must stay in the page key |
| A validator length rule | The matching `HasMaxLength` in `EmploymentVerificationContactConfiguration`, or an over-long value reaches PostgreSQL as a `DbUpdateException` instead of a 400 |
| The dialog's client-side email regex | `ContactDirectoryValidatorTests.AddValidator_ShouldAccept_WhenDomainHasNoDot` — the client must not be stricter than FluentValidation's `EmailAddress()` |
| A route string | `EmploymentVerificationRoutes`, the page's `@page`, the `EVLayout` tab, and `EmploymentVerificationPaths` |
| The seed generator's namespace GUID | Nothing — **do not change it.** Every committed id derives from it |
| `SubMenuList` submenu 9's path | The backend application/submenu seed data, `Home.razor.cs`'s route builder, and the landing redirect |
| A `--c-ev-*` token | Both the `:root` and `html.dark` blocks in `theme.css`; nothing else holds an EV colour literal |

## Tests

`Test/Test/BackendAPI/Modules/EmploymentVerification.UnitTests/`

- `ContactDirectoryServiceTests` — normalisation, the composite cursor round trip, count
  on first page only, undecodable-cursor fallback, page-size clamping, and both
  duplicate/not-found throws. Uses `MockBehavior.Strict`, so an unexpected repository call
  fails the test.
- `ContactDirectoryValidatorTests` — the `|` rule in both commands, email shape, length
  bounds, page-size bounds, and the deliberate dotless-domain acceptance.

There are still **no EmploymentVerification integration tests**; the module has never had
them. The 472 existing integration tests do exercise this migration, because
`IntegrationTestWebAppFactory` runs every migration on a clean Testcontainers PostgreSQL —
which is how the seed is known to apply cleanly. A dedicated EV harness, when written,
must clear **both** EV cache tags (`employmentverification:contacts` and
`…:requests`) alongside truncation, for the reason `BaseIntegrationTest` already
documents: cached first pages and counts otherwise survive the truncate.
