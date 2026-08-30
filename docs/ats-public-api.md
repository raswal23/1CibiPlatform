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

### Step 8 — Documentation site

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

## 3. Tests

| Suite | Result |
|---|---|
| `BulkSubjectRowValidatorTests` (new) | 31 passed |
| `PublicApiRepositoryIntegrationTests` (new, real Postgres) | 13 passed |
| Full ATS suite | 418 passed |
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
