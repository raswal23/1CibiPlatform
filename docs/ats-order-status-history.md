# ATS order status history

ATS now keeps an append-only business lifecycle timeline in `ats.OrderStatusHistory`. This is transactional/business history for requestors, not Serilog application logging.

## Lifecycle events

| Successful action | Event | Previous status | New status |
|---|---|---|---|
| Single order created and invitation sent | `OrderCreated` | — | Pending Candidate Info |
| Subject submits the form | `ApplicationFormSubmitted` | Pending Candidate Info | In Progress |
| Subject cancels the form | `ApplicationFormWithdrawn` | Current status | Application Withdrawn |
| Requestor resends a withdrawn form | `ApplicationFormResent` | Application Withdrawn | Pending Candidate Info |
| Final report is uploaded | `ReportUploaded` | In Progress | Completed |
| Completed report is disputed | `ReportDisputed` | Completed | Completed |

Dispute is an event without an `OrderStatus` transition because ATS currently stores it in `DisputeCategory` and `DisputedAt`. Initial report uploads do not record completion; only an upload that actually moves the order to `Completed` does.

## Architecture

- Repositories must contain only database transaction and persistence logic. They must not contain business processes or business logic; that logic belongs in the service layer.
- Entity and EF configuration live in the ATS module.
- `IOrderHistoryFactory` creates history records consistently, including source, UTC timestamp, and the authenticated user when available.
- Carter endpoint → MediatR query handler → order-history service → repository → `ATSDBContext`.
- The endpoint is authorized and applies the same ATS client/requestor scope as report access.
- The YARP module route is declared in `ATSPaths`.
- The UI calls `GET /ats/getorderstatushistory?emailInvitationRequestId={id}`; the gateway forwards it to the API's static route while preserving the query string.
- The migration is under `BackendAPI/API/APIs/Migrations/ATS`.
- In Search Report, the existing status badge is a button that lazily opens the ATS-themed timeline dialog. The dialog includes loading, empty, failure/retry, withdrawn, resend, completed, and dispute presentations.

## Resend is scope-checked

`ResendApplicationFormAsync` takes a caller-supplied invitation id and is reachable from
more than one screen (Withdrawn applications, and the Bulk Uploads subject drill-down).
It resolves `IAtsAccessScopeResolver` and throws `NotFoundException` when the invitation's
`ClientId`/`RequestorId` fall outside the caller's scope. Out-of-scope and non-existent
are deliberately the same response, so a caller cannot probe which ids exist. Do not add
a new entry point that bypasses that check.

## Adding another lifecycle event

1. Add its stable name to `OrderHistoryEventType`.
2. At the successful business transition, call `IOrderHistoryService.RecordAsync` with the actual previous and new statuses.
3. Add the user-facing title, description, icon, and tone in `OrderStatusHistoryDialog`.
4. Keep technical errors and exception details in PlatformLogging; do not add them to business history.

## Code formatting

Keep ATS feature code vertically structured and easy to scan. Use one property or statement per line, split long method parameters and component attributes across lines, and follow the indentation already used by the ATS module. Do not compress Razor markup, DTO properties, switch expressions, or service logic into horizontal one-line blocks.

Prefix ATS-owned CSS classes with `ats-` so styles remain module-specific and can be moved with the ATS UI when modules are separated.

For Quartz jobs and other execution paths outside MediatR, explicitly set the ATS logging scope where technical logs are produced. Business lifecycle events should still be written through `IOrderHistoryService` with `OrderHistorySource.System` when the job itself causes the transition.
