# ATS Bulk Requeue

Retrying failed work in bulk, on the Bulk Uploads and Ticketing Status boards.

Related: `docs/ats-email-delivery.md` (how requeued invitations are actually sent),
`docs/ats-notifications.md`, `docs/feature-development-guide.md`.

---

## 1. What it does

Both boards park work that failed repeatedly: an invitation whose email could not be
delivered stops at `EmailSentStatus = Error`, and an order OMS rejected stops at
`TicketStatus = Error`. Each row already had a one-at-a-time retry button.

That was fine for one bad address and useless for the case that actually happens. A provider
throttle or an OMS outage fails **every row in the batch at once** — the incident in
`ats-email-delivery.md` left 186 invitations parked — and clicking a row button 186 times is
not a recovery plan.

Both boards now support selecting many failed rows and requeueing them in one action.

## 2. The shared shape

Retry means the same thing everywhere in ATS: **put the row back on its queue with a fresh
attempt budget, and let the background job do the work.** It never sends or tickets inline.

`RequeueExhaustedTicketAsync` established this. The email side was migrated onto it
(`RequeueEmailInvitationAsync`), and both now have a set form:

| | Ticketing | Email |
|---|---|---|
| Single | `RequeueExhaustedTicketAsync` | `RequeueEmailInvitationAsync` |
| Bulk | `RequeueExhaustedTicketsAsync` | `RequeueEmailInvitationsAsync` |
| Endpoint | `PATCH /ats/retrytickets` | `PATCH /ats/resendapplicationforms` |
| Service | `RetryTicketsAsync` | `ResendApplicationFormsAsync` |

### The status predicate is the concurrency guard

In every one of those, the eligibility check lives **inside the `UPDATE`**, not in a
preceding read:

```csharp
.Where(x => ids.Contains(x.EmailInvitationID)
         && !x.IsTicketed
         && x.TicketStatus == TicketStatus.Error
         && x.TicketAttempts >= MaxTicketAttempts)
.ExecuteUpdateAsync(...)
```

A read-then-write would race the background job and could resurrect a live claim. Doing it
this way means a row the job re-claimed a moment ago simply does not match, and the returned
count is what genuinely moved.

### A partly-stale selection is not an error

This is the design decision most likely to be "corrected" by mistake.

A selection is made against rows rendered seconds or minutes ago. By the time the operator
clicks, the job may legitimately have picked some of them up. Failing the whole request
because of that would punish the operator for the system working.

So ineligible ids are **skipped, not rejected**, and the response carries both numbers:

```csharp
public sealed class BulkRetryResultDTO
{
    public int RequestedCount { get; set; }
    public int RequeuedCount { get; set; }
    public bool IsComplete => RequeuedCount == RequestedCount;
}
```

The UI reports "3 of 5 queued. The rest were already being sent." as `Severity.Info` — a
normal outcome, not a failure.

The one case that does throw is `NotFoundException` when **nothing** in the selection is
available to the caller, because that means the request was meaningless.

### Scope is enforced per row

`GetRetryTargetsAsync` and `GetEmailInvitationOwnersAsync` read the client/requestor
identity of every id **before** the update, and out-of-scope ids are dropped.

Without this, a bulk endpoint becomes a way to touch another client's records by posting
their ids alongside your own. Out-of-scope ids are dropped **silently** rather than reported,
for the same reason the single-row paths answer `404` instead of `403`: naming them would
confirm those records exist.

### Batch cap

| Screen | Constant | Value |
|---|---|---|
| Ticketing | `OMSTicketingMonitoringService.MaxBulkRetrySize` | 500 |
| Email | `EndorsementSubmissionService.MaxBulkResendSize` | 500 |

Enforced in the FluentValidation validator (so an oversized request is a `400` before any
database round trip) **and** in the service (where the reason lives).

The reason is downstream load. Every requeued invitation becomes a message on the
deliberately-paced email queue — at 0.9 sends/second, 500 invitations is already about nine
minutes of sending. Every requeued order becomes an OMS round trip. Releasing thousands at
once would let one operator's click monopolise a shared job and block every other client.

## 3. The selection bar

The action lives in **its own band between the filter chips and the table**, not in the
table's toolbar. It appears only when something is selected, showing the count, a `Clear`
button and the requeue button.

This is a layout decision with a reason. The first version put the button in the toolbar's
left slot beside the date filter, which forced it to align with the search box and reload
button and pushed both out of shape the moment a selection existed — the button ended up
overlapping the table header. As a separate band it can appear, grow and disappear without
disturbing any other control; the table simply moves down while it is open.

It carries `aria-live="polite"` so a keyboard or screen-reader user hears the count change
as they select, since the bar appears and updates without any navigation.

**It is `position: sticky`.** A selection made at the top of a long list would otherwise
scroll out of reach, forcing the operator back up to act on it. Two consequences worth
knowing before changing it:

- **Its background must stay opaque.** The accent tint is translucent, so it is layered over
  `var(--c-surface)` with a `linear-gradient`. Dropping the surface layer lets table rows
  show through while scrolling.
- **No ancestor may set `overflow`.** Sticky silently stops working if one does.
  `.ats-console`, `.ats-console-main` and `.ats-console-content` all leave it unset today;
  the dialog sticks to `.ats-dialog-body`, which is the scroll container there.

Below 600px it reverts to `position: static` — stacked it is about two rows tall, which on a
phone would cover too much of the visible list for the whole scroll.

The Ticketing board uses `.ats-status-board-selection-bar` from `ats.css`. The Bulk Uploads
dialog restates the same rules locally as `.ats-bulk-subjects-selection-bar`, because it is
not rendered inside `.ats-management-page` and so cannot match that rule's page anchor —
**keep the two in step.**

## 4. Selection is per page, on purpose

The header checkbox selects **only the eligible rows on the current page**, not everything
matching the filter.

Selecting rows the operator has not looked at is how a click ends up retrying far more than
intended. If they want more, they page and select again — deliberate, visible, and bounded
by the cap either way.

Two supporting rules:

- **Only eligible rows get a checkbox.** Selecting a row the server would refuse produces a
  confusing "0 of N" and nothing else. `CanResend` / `CanRetry` gate both the checkbox and
  the row button, so the UI offers exactly what the API will accept.
- **The selection is pruned on every reload.** Ids are held in a `HashSet<Guid>`, and any id
  that is no longer on screen — filtered out, paged past, or no longer eligible — is dropped
  in `LoadSubjectsAsync` / `LoadOrdersAsync`. Keeping it would let an operator submit rows
  they cannot see.

## 5. Files

```text
Backend
  Features/Web/OMSTicketing/Command/RetryTickets/       endpoint + handler + validator
  Features/Web/ResendApplicationForms/                  endpoint + handler + validator
  Services/OMSTicketingMonitoring/                      RetryTicketsAsync
  Services/EndorsementSubmission/                       ResendApplicationFormsAsync
  Data/Repository/OMSTicketing/                         RequeueExhaustedTicketsAsync, GetRetryTargetsAsync
  Data/Repository/EmailInvitations/                     RequeueEmailInvitationsAsync, GetEmailInvitationOwnersAsync
  DTO/BulkRetryResultDTO.cs, DTO/EmailInvitationRequeueDTO.cs
  Path/ATSPaths.cs                                      both gateway routes

Frontend
  Component/ATS/OMSTicketing/TicketingStatusComponent   checkbox column + selection bar
  Component/ATS/BulkUploads/BulkUploadSubjectsDialog    checkbox column + selection bar
  Services/ATS/OMSTicketing/                            RetryTicketsAsync
  Services/ATS/EndorsementSubmission/                   ResendApplicationFormsAsync
  DTO/ATS/BulkRetryResultDTO.cs
  wwwroot/css/ats.css                                   .ats-status-board-select-*,
                                                        .ats-status-board-selection-*,
                                                        .ats-status-board-requeue
```

## 6. How to verify it

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ResendApplicationForm"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~OMSTicketingRepository"
dotnet build 1CibiPlatform.sln
```

The tests that pin the design decisions:

- `RequeueExhaustedTicketsAsync_ShouldSkipIneligibleOrders_WithoutFailingTheBatch`
- `ResendApplicationForms_ShouldSkipInvitationsThatAreMidSend`
- `ResendApplicationForms_ShouldIgnoreInvitationsOutsideTheCallerScope`
- `ResendApplicationForms_ShouldRequeueEveryInvitation_WithItsOwnToken`

Confirm both routes are served:

```text
GET /__routes
```

Manually: fail some rows, select several, requeue, and watch them move to `Pending` with a
cleared attempt count — that visible reset is the confirmation the retry took effect.

## 7. What not to do

- **Do not give each requeued invitation the same token.** `RequeueEmailInvitationsAsync`
  runs one `UPDATE` per row precisely so each carries its own. A shared token would let any
  candidate in the batch open another candidate's application form.
- **Do not fail the request because part of the selection is stale.** Skipping and reporting
  the count is the contract; throwing would make the button unusable exactly when a large
  batch is moving.
- **Do not move the eligibility check out of the `UPDATE`.** A read-then-write races the
  background job.
- **Do not enforce scope once for the request.** It is per row, or the endpoint leaks across
  clients.
- **Do not extend select-all across the whole filter** without also showing the operator
  what they are about to touch. The cap bounds the damage; visibility is what prevents the
  mistake.
- **Do not move the action back into the table toolbar.** It has to share that row with the
  search box and the reload button, which are pinned to a fixed height for alignment; the
  button either breaks that alignment or gets pushed onto the table header.
- **Do not raise the batch caps to "make it faster".** They exist because the queues
  downstream are deliberately paced. A bigger batch does not send faster; it just blocks
  other clients for longer.
