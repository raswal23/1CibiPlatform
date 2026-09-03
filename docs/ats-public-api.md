# ATS Public API + Developer Documentation — Implementation Review

Separates the ATS module by trust boundary, exposes the console's operations as a public
API for client integrations, and publishes a public documentation site describing it.

Follows `docs/feature-development-guide.md`. Structural precedent:
`BackendAPI/Modules/EmploymentVerification/`.

---

## 1. Two things that made this smaller than expected

**Authentication already existed.** `/token/generatetoken` → `Auth.Features.Login` issues
the same JWT the web app uses, and `LoginService.LoginAsync` calls `AddAtsClaimsAsync`
before minting it — so the token already carries `AtsClientId` and `AtsRoleId`. A
public-API caller is an ordinary authenticated principal. `ICurrentUser` and
`AtsAccessScopeResolver` work unchanged, and the package-entitlement scoping already in
place applies as-is. **No API keys, no new auth middleware, no gateway auth work.**

**`OrderHistorySource.PublicApi` already existed** and was unused, as did the `source`
parameter on `IOrderHistoryService.RecordAsync`. Wiring it up was threading, not design.

**AI is excluded**, as specified — the assistant stays UI-only.

---

## 2. Step by step

### Step 1 — Restructure (its own commit, zero behaviour change)

```text
BackendAPI/Modules/ATS/Features/
  Web/          ← all 27 existing folders, moved verbatim
  PublicApi/    ← new
```

106 files re-namespaced to `ATS.Features.Web.*`; git tracked every one as a rename, so
history is preserved. Nothing outside the module referenced these namespaces except 10
test files. Route literals are unchanged, so no gateway or UI change fell out of the move.

**374 tests passed and the solution built before any feature work started.**

### Step 2 — The source flag

`InsertEmailInvitationRequestAsync` and `InsertBulkSubjectAsync` gained an optional
`source` parameter defaulting to `OrderHistorySource.Web`, so every existing caller is
untouched. Public slices pass `OrderHistorySource.PublicApi`.

For bulk this needed more than a parameter: the CSV is parsed by a Quartz job long after
the upload returns, so the source is persisted on `BulkUploadFileDetails.Source` and read
back by the job when it creates each order.

### Step 3 — CSV row validation

`BulkSubmissionProcessorService` validated the CSV **header set and nothing else**. Row
values went into the database unchecked, so a blank email or malformed mobile number was
inserted and only failed much later — at email send, or at OMS ticketing where it parked
as an opaque error. The single-endorsement path validated all of this; the bulk path did
not.

New `BulkSubjectRowValidator`, pure and static, with the rules the single-endorsement
validator already applies:

| Column | Rule |
|---|---|
| `FirstName` | required, ≤ 50 chars |
| `LastName` | required, ≤ 50 chars |
| `MiddleInitial` | **no constraints at all** — blank is valid and expected |
| `EmailAddress` | required, valid email |
| `MobileNumber` | required, normalised to 11 digits |

A failing row is **skipped and reported, not fatal** — one bad row must not reject a
file of good ones.

**On the mobile number:** the first cut required exactly 11 digits, which broke three
existing tests whose fixtures use `+639171234567`. That is the same number as
`09171234567`, and `OMSTicketPayloadMapper` already converts between the two. Rejecting
it would have forced integrators to reformat valid data, so the validator now
**normalises then validates** — `+63…`, `63…`, a bare `9…`, spaces and dashes all reduce
to the local form, which is what gets stored. Anything that cannot be reduced to 11
digits is still rejected.

**This changes the existing web bulk upload too**, by design: rows that silently entered
before are now skipped and reported.

### Step 4 — Two gaps found while doing that

**Bulk orders wrote no order history at all.** Single orders recorded `OrderCreated`;
bulk orders recorded nothing, so their timelines started blank and there was no existing
call to attach a source to. The job now records `OrderCreated` per order via a new
`RecordManyAsync` — one insert for the whole file, because `AddAsync` saves per row and a
500-row upload would have meant 500 round trips.

**`OrderHistoryFactory` reads `ICurrentUser`, which is null on a Quartz thread.** Bulk
history would have lost the uploader entirely. `Create` now takes an optional
`changedByUserId`, and the job passes `file.UploadedByUserId` — captured at upload time
for exactly this reason.

### Step 5 — Where rejected rows live

Three columns on `BulkUploadFileDetails`: `AcceptedRowCount`, `RejectedRowCount`,
`RejectedRows` (JSON), plus `Source`. Written **once** by the parsing job; the status
endpoint is a single primary-key read.

They are stored rather than computed because the rejected rows were never inserted —
there is no table to count them from. Storing the outcome is what makes the read possible
at all, not merely faster.

Migration `AddBulkUploadSourceAndRowOutcomeATSMigration` — four additive columns, nothing
destructive.

### Step 6 — The endpoints

All under `api/public/ats/...`, gateway-mapped to `/publicapi/ats/...`, all
`.RequireAuthorization()`.

| Operation | Route | Reuses |
|---|---|---|
| Create endorsement | `POST /endorsements` | `InsertEmailInvitationRequestAsync` |
| Create bulk endorsement | `POST /endorsements/bulk` | `InsertBulkSubjectAsync` |
| Bulk upload result | `GET /endorsements/bulk/{fileId}` | new read |
| My packages | `GET /packages` | `GetPackagesAsync(…, clientId)` |
| List orders | `GET /orders` | `GetReportsAsync` |
| Get order | `GET /orders/{orderId}` | new read |
| Download documents | `POST /orders/{orderId}/report` | `DownloadIndividualReportAsync` |
| Withdraw order | `PATCH /orders/{orderId}/withdraw` | new write |

Two decisions worth flagging:

- **Bulk returns `202 Accepted`, not `200`.** The file is queued and parsed seconds
  later; claiming success at upload time would be a lie. The response carries the
  `fileId` to poll. Getting that id out meant writing it back onto the DTO rather than
  changing the service's return type and touching every web caller.
- **`GET /packages` takes the client from the token**, never from the request — a caller
  must not be able to read another client's entitlements by passing an id.

### Step 7 — Withdraw is the one that is not a wrapper

The existing `PATCH withdrawnapplicationform` is anonymous and keyed on `HashToken` — it
is the *candidate* withdrawing from their emailed link. **There is no cross-client leak
there**: the repository filters on the token, which is an 86-char secret sent only to
that candidate, and the withdrawn *list* is properly client-scoped via
`IAtsAccessScopeResolver`.

A client withdrawing their own order by id is a different operation, so
`PublicApiRepository.WithdrawOrderAsync` is new. Scope and terminal-state guards live in
the `UPDATE` predicate rather than a preceding read, so a concurrent completion or a
second call updates nothing instead of racing.

### Step 8 — Package and order type were never validated

Found during review, and it predates this work: **every order-creation path accepted any
text as a package or an order type.** Both validators checked string length only, so
`package: "banana"` and `orderType: "Whenever"` were stored happily. The mistake surfaced
much later at OMS ticketing, where an unmatched package parks the order as an `Error`
with an opaque reason — or, for a bogus order type, never surfaced at all.

The AI assistant plugin already did this correctly (`AtsAssistantPlugin`, client-scoped
package matching plus order-type normalisation); the web and API paths simply skipped it.

Now:

- **`Constants/OrderType.cs`** — `Normal` / `Rush` / `All` plus a `Normalize` helper.
  Previously these were string literals repeated across the AI plugin, the dashboard and
  the CSV parser, with nothing rejecting a third value.
- **`Services/OrderValidation/OrderInputValidator`** — one implementation shared by the
  web console, the public API and the bulk parser, so all three agree. It checks the
  order type, then that the package is active and **assigned to the caller's client**,
  resolved from their token via `ICurrentUser.AtsClientId` — never from a request field.
- Rejections throw `BadRequestException` **naming the acceptable values**: the client's
  own package names, which they can already read from `GET /packages`. A rejection the
  caller can act on beats a correct one they cannot.
- Both values are **canonicalised on the way in**. `"criminal records check"` is stored
  as `"CRIMINAL RECORDS CHECK"`, `"rush"` as `"Rush"`. This is not cosmetic: OMS matches
  the package by exact name, so a casing difference would have parked the ticket.

The order type is additionally checked in the endpoint validators, where it costs no
database round trip; the package can only be checked against the caller's client, so it
stays in the service.

**Two consequences worth flagging at review:**

- This changes the **existing web console**, not just the new API. It is the fix, but it
  is a behaviour change.
- Two pre-existing integration tests failed immediately, because they created orders
  against packages that were never assigned to anyone. That is the validation working.
  They now seed a package via a new `BaseIntegrationTest.SeedAssignedPackageAsync`, and
  the fake test principal gained the `AtsClientId` claim it had been missing.

**Orders now reference their package by id** — see Step 11 below. The name-based link was
fixed rather than left for later, because nothing is in production yet and the public API
contract had not shipped.

### Step 9 — A better create response

`POST /endorsements` returned a bare `true`, which tells a caller nothing. It now returns:

```json
{
  "isSuccessful": true,
  "orderId": "0199a1c4-...",
  "package": "CRIMINAL RECORDS CHECK",
  "orderType": "Normal",
  "message": "The order was created and the application form has been emailed to the subject."
}
```

The `orderId` is the point — without it an integrator had to search for the order they
had just created. Package and order type are echoed as **stored**, so a caller who sent
`"rush"` can see it became `"Rush"`. Surfacing the id meant writing it back onto the DTO
rather than changing the service's return type and touching every web caller.

### Step 10 — Documentation site

`/docs/api`, public and unauthenticated. **The auth opt-out is one line:**
`@layout GenericLayout`. `App.razor` defaults to `MainLayout`, whose `OnInitializedAsync`
is the only redirect-to-login in the app. No `SecurePageBase`, no `[RequirePermission]`.
Nav exclusion is automatic — no entry was added to `ApplicationList.cs` or `ModuleList.cs`.

Three-column reference layout: sticky contents rail, scrollable body, per-endpoint sample
panel with a cURL/C# switcher and copy-to-clipboard. Content is a **typed C# model**
(`ApiDocsContent.cs`) rather than markdown files — compiler-checked, and it avoids an
nginx trap: `.md` has no `location` block, so a mistyped path returns `index.html` with
HTTP 200 instead of a 404.

**CSS followed the reuse rule.** The `.code-block` / `.json-*` rules were generalised out
of `PlatformLogs.razor.css` into `wwwroot/css/ats.css` as `.ats-code-block` /
`.ats-json-*`, and **PlatformLogs was migrated onto them in the same change** — its
scoped sheet lost 54 lines of now-shared rules. The language switcher reuses
`.ats-segmented` / `.ats-segment-btn` as-is.

---

### Step 11 — Orders reference their package by id

The package validation above closed "any text is accepted", but not the other half: the
order → package link was still a **string comparison**, so renaming a package silently
orphaned every order that referenced it. Those rows kept the old string,
`OMSTicketingRepository.GetTicketPayloadsAsync` stopped matching them, and they parked as
`Error` with a reason nobody could act on. Nothing prevented or warned about it.

Done now rather than deferred to 1Platform for one reason: **the public API contract had
not shipped.** Once integrators are sending package names, changing the relationship is a
breaking change on someone else's schedule.

- `EmailInvitationRequest.PackageId` and `BulkUploadFileDetails.PackageId`, both **FK to
  `PackageDetails` with `ON DELETE RESTRICT`** — removing a package must never delete the
  orders placed under it.
- **`SelectPackage` / `PackageType` are kept** as denormalised display labels. Of the ~65
  references to them, the large majority are reads — report lists, search, exports, the
  ticketing screen, the AI draft card — and all of those keep working untouched. Only
  about ten files changed.
- `OrderInputValidator` already resolved the package to validate it; it was simply
  discarding the id. `ValidatedOrderInput` now carries `PackageId` alongside the name, and
  the three write paths store both.
- **The one real logic change** is the ticketing join, from `PackageName == SelectPackage`
  to `PackageId == invitation.PackageId`. That is what makes a rename safe.
- `EditPackageAsync` now **refreshes the label** on every order and bulk file referencing a
  renamed package, and raises `NeedsProjection` so the denormalised search rows rebuild.
  A rename went from "orphans orders" to "updates orders".

**The API contract is unchanged** — callers still send `package` by name, and the server
resolves it to an id. The name is the friendlier contract, and with the FK the
relationship is sound either way. `GET /packages` returns `packageId` for integrators who
want to pin to it later.

**Migration and test-harness impact:** the FK made 128 existing integration tests fail
immediately, because they seeded orders with no package. Rather than patch each,
`BaseIntegrationTest` now creates one package after every truncate — named so it sorts
last, and inserted via raw SQL so it is untracked and cannot collide with a test's own
packages. The seed data was also changed to resolve package ids, skipping any seed row
whose package does not exist.

The database side is documented separately in **`docs/package-id-migration.md`**, which
covers the pre-flight count, what to do with existing data, verification queries and
rollback.

## 3. Tests

| Suite | Result |
|---|---|
| `BulkSubjectRowValidatorTests` (new) | 31 passed |
| `OrderInputValidatorTests` (new) | 21 passed |
| `PublicApiRepositoryIntegrationTests` (new, real Postgres) | 13 passed |
| Full ATS suite | 442 passed |
| Auth suite | 232 passed |
| `dotnet build 1CibiPlatform.sln` | succeeded |

The validator tests pin the requirement you asked for explicitly: **a blank middle name
is accepted** (null, empty and whitespace all covered), while blank email, missing
surname and an unusable mobile number are each rejected with a reason.

The integration tests run against a real Postgres container and cover the scoping that
matters: another client's order returns null (→ 404), another user's order returns null,
an unrestricted caller sees everything, withdraw refuses an already-terminal or
out-of-scope order, and two withdraw calls do not both succeed.

---

## 4. Not done / worth knowing

1. **No live end-to-end run.** Every documented sample is written but none has been
   executed against a real environment. The samples must be run verbatim before this is
   published — a docs site whose examples were never run is the usual failure mode.
2. **`DefaultStrict` is one global bucket.** It partitions on the policy name, not the
   caller, so 20/min is shared across every public-API client. This is its first use by
   any route. A per-token partition is the real fix.
3. **Withdraw-by-token has no expiry check.** Unlike form submission, which calls
   `AuthorizeApplicationFormAsync` and checks `IsExpired`, the candidate withdraw path
   goes straight to the token lookup — so an old link still works. Agreed to fix in a
   separate PR.
4. **The base URL in the docs is a placeholder** (`https://api.cibi.com.ph`). Confirm the
   real public host before publishing.
5. **No versioning.** Public routes are versionless. A breaking change should add a new
   path rather than alter an existing response shape — integrators cannot be redeployed
   on our schedule.
6. **Swagger stays Development-only**, as the existing deliberate decision has it. This
   docs site is the public substitute.
7. **The migration assumes empty order tables.** `PackageId` is added `NOT NULL` with a
   default of `0`, which no package has — so on a database with existing orders the FK
   will fail. That is deliberate, agreed while none of this is in production. Anyone
   applying it elsewhere must read `docs/package-id-migration.md` first, which has the
   pre-flight count and a backfill variant for a populated database.
8. **The bulk CSV's own columns are unvalidated at upload time.** The package and order
   type are checked before the file is stored, but a wrong header row is only discovered
   when the job parses it — reported through the bulk status endpoint rather than the
   upload response. That is inherent to parsing asynchronously.
