# Employment verification request tracking

The Employment Verification page now separates the two datasets it works with: ATS candidates that still need a verification email, and the requests this module has already raised. Previously one table tried to be both.

## Request lifecycle

| Action | Status | Timestamp set |
|---|---|---|
| Request row created before the email is attempted | `Pending` | `RequestedAt`, `TokenExpiresAt` |
| Verification email accepted by the email service | `Sent` | `SentAt` |
| HR contact confirms the details | `Verified` | `VerifiedAt` |
| HR contact reports the details inaccurate | `Rejected` | `RejectedAt` |

`Expired` exists on `VerificationRequestStatus` but is never written. A lapsed link is derived as `Status == Sent && TokenExpiresAt < now`, so no background sweep is needed to keep the list correct. The UI labels such a row `Expired` for display only; the stored status stays `Sent`.

## Candidate availability rule

`GetAvailableATSRecordsAsync` withholds an ATS candidate while a request is awaiting a response or already settled successfully, and releases them otherwise:

| Latest request state | Candidate offered for a new request |
|---|---|
| `Pending` | No |
| `Sent`, link still valid | No |
| `Verified` | No |
| `Rejected` | Yes |
| `Sent`, link lapsed | Yes |
| No request at all | Yes |

Releasing on rejection and expiry means a bounced or unanswered request can be re-sent from the UI without a database edit. The comparison instant is passed in as `asOfUtc` rather than read inside the query, which keeps the repository free of clock decisions and makes the rule testable.

## Architecture

- Repositories contain only persistence. Write methods save internally and return `Task<bool>`; the repository interface does not expose `SaveChangesAsync`. This matches `ATSRepository` and the focused Auth repositories.
- The availability rule is business logic and lives in `EmploymentVerificationService`, not in the repository or the query handler.
- `ListBlockedAtsSubjectIdsAsync` filters and projects in SQL, returning only the blocking subject ids rather than loading request rows.
- Status transitions use `ExecuteUpdateAsync` (`MarkSentAsync`, `MarkRespondedAsync`) instead of mutating a tracked entity and re-saving.
- `MarkRespondedAsync` restricts its update to a row that is still `Pending` or `Sent`. Single use is enforced by that predicate, so two simultaneous clicks on the emailed link cannot both record a response; the losing call reports `AlreadyCompleted`.
- The cache decorator invalidates `RequestsTag` inside each write method, gated on the returned bool, following `ATSCacheRepository`. `ListBlockedAtsSubjectIdsAsync` is deliberately uncached because its result depends on how the supplied instant compares to each token expiry.
- Carter endpoint → MediatR query handler → `IEmploymentVerificationService` → repository → `EmploymentVerificationDbContext`.
- The YARP module route is declared in `EmploymentVerificationPaths`.

## Public contracts

| Gateway route | Backend route | Returns |
|---|---|---|
| `GET /employmentverification/getatsinprogress` | `/api/employment-verification/ats/in-progress` | ATS candidates with no blocking request |
| `GET /employmentverification/getsentrequests` | `/api/employment-verification/requests/sent` | `SentVerificationRequestDTO` list |

`SentVerificationRequestDTO` deliberately excludes `VerificationTokenHash`. That hash is the credential embedded in the emailed link, so a list endpoint must not return it.

**Known gap:** the older `GET /employmentverification/getrequests` still returns the `EmploymentVerificationRequest` entity directly, including `VerificationTokenHash`. It is authenticated but any authorised caller can read every live token. Replace its projection before relying on that route.

The typed `EmploymentVerificationPaths` module is the only wiring, and that is correct — it is how every module routes. The gateway loads routes exclusively from `IReverseProxyModule` implementations via `LoadFromMemory`; the `ReverseProxy:Routes` section in the gateway appsettings is dead configuration with no reader. Do not add `employmentverification` entries there. See `docs/feature-development-guide.md` §7a.

## UI

> **Restructured twice.** The single `EmploymentVerification.razor` page described here was
> split into separate routes under a top-navbar shell and rethemed from magenta to teal
> (see
> [employment-verification-contact-directory.md](../../employment-verification-contact-directory/employment-verification-contact-directory.md)),
> and **sending is now automatic** (see
> [employment-verification-auto-send.md](../../employment-verification-auto-send/employment-verification-auto-send.md)).
>
> Two consequences for what follows. The Needs-request view is **read-only** — a scheduled
> job sends, so there is no Send button and no review drawer. And a request now covers one
> **employment segment** rather than a whole order: a candidate with three former employers
> produces three independent requests, each with its own token and outcome.

The two views are separate routes under `Layout/EVLayout.razor`, each a
`.razor` / `.razor.cs` trio inheriting `CrudPageBase`:

| Route | Page | Columns |
|---|---|---|
| `/employmentverification/requests` | `NeedsRequest.razor` | candidate, employer no., previous employer, employment period, supervisor email, status |
| `/employmentverification/tracking` | `Tracking.razor` | candidate, previous employer, sent to (+ source), requested, responded, status |

Needs request is a **queue**, one row per employment segment, answering "why has this
employer not been contacted yet?" Its status chips — Queued, No consent, Needs recipient —
are **derived**, not stored: they describe a segment ATS offers that has no request row.
Tracking's "Sent to" column shows both the address and whether it came from the vetted
directory or from the candidate, which is what makes a returned confirmation auditable.

`/employmentverification/verification` — the path registered as submenu 9, and therefore
the one the home application card links to — remains as a redirect to the Needs request
tab, so the card, existing bookmarks and the backend application/submenu seed data are all
untouched.

Needs request has no `Requested` column: that value belongs to a verification request, not
to an ATS record, so showing it there was misleading.

Both pages use the shared `TableComponent` (search, reload, skeleton loading rows, 960px
card mode) instead of the hand-written `<table class="ev-table">` they used to, and the
shared `.ats-status-pill` classes for status chips. They still filter client side, because
both endpoints return the whole list in one call. The response-rate tile now lives on
Tracking, where its numbers come from, and still reports an em dash until something has
been sent rather than `0%`.

The "send verification email" review panel is deliberately still a drawer rather than a
MudDialog — it is a read-and-send panel, not a form — and backdrop clicks still do not
close it.

`SecurePageBase` supplies the `[RequirePermission(8, 9)]` check in `OnInitializedAsync`. A page overriding that method must call `base.OnInitializedAsync()` and return early when `IsPageAuthorized` is false, otherwise the permission attribute is silently inert.

## Code formatting

Keep Employment Verification code vertically structured: one property or statement per line, long parameter lists and object initializers split across lines. Do not compress Razor markup, DTO properties, or service logic into one-line blocks.

Prefix module-owned CSS classes with `ev-` so styles stay module-specific, and put them in
`wwwroot/css/ev.css` rather than a scoped `.razor.css` when more than one page or component
needs them.

The module's accent is **teal** (`--c-ev-accent` and friends in `theme.css`), not the
magenta it used to be, and not a second copy of the ATS blue. The always-dark surfaces —
navbar, dialog headers, primary buttons — share the ATS navy tokens exactly; only the
accent differs, which is what makes the two applications read as siblings. The private
`--pink` / `--pink-dark` / `--ink` aliases are gone.

New screens should use `TableComponent`, `CrudPageBase` and the shared `.ats-management-*`
and `.ats-dialog-headline` families rather than bespoke markup. `ev.css` re-points the
`--management-*` properties those shared rules read, so they render teal inside this module
with no rule copied.
