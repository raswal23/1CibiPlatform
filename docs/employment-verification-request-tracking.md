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
- Carter endpoint â†’ MediatR query handler â†’ `IEmploymentVerificationService` â†’ repository â†’ `EmploymentVerificationDbContext`.
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

`Pages/EmploymentVerification/EmploymentVerification.razor` presents one table at a time behind a segmented switcher, following the `ats-segmented` pattern in `Component/ATS/Settings/Settings.razor`:

- **Needs request** â€” candidate, previous employer, employment period, HR email. No `Requested` column: that value belongs to a verification request, not to an ATS record, so showing it here was misleading.
- **Tracking** â€” candidate, previous employer, HR email, requested, responded, status.

Both views render an empty state and replace the table while loading rather than showing an empty body. The response-rate tile is computed from answered requests and reports an em dash until something has been sent, instead of `0%`.

`SecurePageBase` supplies the `[RequirePermission(8, 9)]` check in `OnInitializedAsync`. A page overriding that method must call `base.OnInitializedAsync()` and return early when `IsPageAuthorized` is false, otherwise the permission attribute is silently inert.

## Code formatting

Keep Employment Verification code vertically structured: one property or statement per line, long parameter lists and object initializers split across lines. Do not compress Razor markup, DTO properties, or service logic into one-line blocks.

Prefix module-owned CSS classes with `ev-` so styles stay module-specific. The module uses its own magenta palette (`--pink`, `--pink-dark`, `--ink`) rather than the ATS navy, and custom markup rather than `TableComponent`/`CrudPageBase`; keep new work consistent with whichever of those the surrounding file already uses.
