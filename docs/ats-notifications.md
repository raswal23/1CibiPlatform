# ATS In-App Notifications

The bell in the ATS topbar, its dropdown, and the full notifications page. Live over
SignalR, backed by a persisted table, pruned after 30 days.

This document is written so you can follow the feature **without reading the codebase**.
Every class is listed with what it holds and who calls it.

---

## 1. The one-paragraph version

Something happens to an order (a candidate submits their form, a bulk file finishes, a
report lands, ticketing gives up). The service that just did that work calls
`IAtsNotificationService`. That **writes a row** to `ats.Notifications` and **then pushes**
the same object down the existing `ATSHub` SignalR connection to the one user it belongs
to. The browser's `NotificationService` increments its badge and raises an event; the bell
re-renders and shows a toast. Clicking the notification marks it read and navigates to the
screen it points at.

Persist-then-push is the whole design. The push only reaches somebody who is looking right
now; the row is what makes it survive a refresh, a logout, or a weekend.

---

## 2. End-to-end trace: "candidate submitted the form"

This is the headline case. Follow it once and the rest are the same shape.

```
Candidate submits the application form
  └─ AddApplicationFormDataEndpoint            (Features/Web/AddApplicationFormData/)
     └─ AddApplicationFormDataHandler          MediatR command handler
        └─ ApplicationFormService.AddApplicationFormDataAsync()
           ├─ ... saves the form, commits the transaction ...
           └─ _notificationService.RaiseForOrderAsync(       ← AFTER the commit
                  emailInvitationId,
                  AtsNotificationType.ApplicationFormSubmitted)
              │
              └─ AtsNotificationService.RaiseForOrderAsync()
                 ├─ repository.GetOrderTargetAsync(orderId)
                 │     reads RequestorId + FirstName/LastName off EmailInvitationRequest
                 ├─ BuildOrderMessage()  → title + body text
                 ├─ BuildOrderLink()     → "/s&i/ats/searchreport?search=Juan%20Dela%20Cruz"
                 └─ RaiseAsync(...)
                    ├─ repository.AddAsync(notification)          ← 1. PERSIST
                    └─ hubContext.Clients
                          .Group(recipientUserId.ToString())
                          .ReceiveNotification(dto)               ← 2. PUSH
                                    │
                                    ▼  (SignalR, /hubs/atsbulk)
Browser
  └─ NotificationService  "ReceiveNotification" handler
     ├─ UnreadCount++
     └─ NotificationsChanged?.Invoke(notification)
        ├─ NotificationCenter  → badge re-renders, toast appears
        └─ NotificationsPage   → row inserted at the top (if open)
```

**Why the call sits after `CommitAsync`.** The candidate's submission is the thing that
matters and it is durable by that point. Notifying is a follow-up; if it were inside the
transaction, a notification failure would roll back a submitted application form.

---

## 3. Backend classes

All under `BackendAPI/Modules/ATS/`.

### Entity — `Data/Entities/AtsNotification.cs`

One row per notification. Not foreign-keyed to anything: it records who was told at that
moment and has to survive the recipient being deactivated or the order being purged.

| Property | Type | What it is |
|---|---|---|
| `NotificationId` | `Guid` | v7, minted in code. Sorts by creation, and is the keyset tie-breaker. |
| `RecipientUserId` | `Guid` | The ATS user this is for. **Also the SignalR group name** — that is what makes the stored row and the live push agree. |
| `Type` | `string` | One of `AtsNotificationType`. Stored as text so a row stays readable and reordering can't retype history. |
| `Title` | `string` | Bold line. Max 160. |
| `Body` | `string` | Detail line, clamped to 2 lines in the UI. Max 500. |
| `LinkUrl` | `string?` | App-relative path to open on click. Null = not clickable. |
| `EntityId` | `Guid?` | The order or bulk file it is about. Kept apart from `LinkUrl` so a future screen can group by subject without parsing a URL. |
| `IsRead` / `ReadAt` | `bool` / `DateTime?` | Badge state. |
| `CreatedAt` | `DateTime` | UTC. Primary sort key and what retention prunes on. |

### Configuration — `Data/EntityConfiguration/AtsNotificationConfiguration.cs`

Table `ats.Notifications`. Three indexes, each earning its place:

| Index | Serves |
|---|---|
| `(RecipientUserId, CreatedAt DESC, NotificationId DESC)` | The feed's fixed ordering — makes a keyset page an index scan, not a sort. |
| `(RecipientUserId, IsRead)` | The unread badge, read on every page load. |
| `(CreatedAt)` ascending | The retention sweep, which deletes oldest-first. |

### Constants — `Constants/AtsNotificationType.cs`

`ApplicationFormSubmitted`, `BulkUploadCompleted`, `OrderCompleted`, `ReportReady`,
`OrderDisputed`, `TicketingFailed`, `InvitationEmailFailed`.

Mirrored on the frontend by `ShareData/ATS/AtsNotificationTypes.cs`, which the UI matches
on to pick an icon and accent colour. Adding one server-side without adding it there is
safe — the row renders with a neutral bell — but it will look undifferentiated.

### DTOs — `DTO/AtsNotificationDTO.cs`

- **`NotificationListDTO`** — what the endpoints return *and* what SignalR pushes. Same
  shape on both paths deliberately, so the client has one code path for a live arrival and
  a fetched row. `RecipientUserId` is **not** on it: you can only read your own.
- **`NotificationUnreadCountDTO`** — `{ UnreadCount }`.
- **`NotificationOrderTargetDTO`** — internal. `RequestorId` + `FirstName`/`LastName`, with
  a computed `SubjectName`. Never returned by an endpoint.

### Repository — `Data/Repository/Notifications/`

`IAtsNotificationRepository` / `AtsNotificationRepository`. **Uncached and undecorated** —
the bell exists to show what just happened, so a cached count would hide the notification
raised a second ago. (Same call as `AtsAuditRepository` and `OMSTicketingRepository`.)

| Method | Notes |
|---|---|
| `AddAsync` | Insert + `SaveChangesAsync`. |
| `GetNotificationsPageAsync` | Keyset page. Takes `take = pageSize + 1` so the service can tell if another page exists without a second query. |
| `CountNotificationsAsync` | First page only. |
| `GetUnreadCountAsync` | The badge. |
| `MarkAsReadAsync` | **Recipient is part of the `WHERE`**, so another user's id matches zero rows rather than being fetched and rejected. |
| `MarkAllAsReadAsync` | Same, in bulk. |
| `GetOrderTargetAsync` | Narrow projection over `EmailInvitationRequest` — two columns, not the whole entity with its seven navigations. |

Every read takes `recipientUserId` first and filters on it. **That is the isolation
boundary**, enforced here rather than left to each handler to remember.

### Service — `Services/Notifications/AtsNotificationService.cs`

The only class that writes notifications.

- **`RaiseAsync(recipient, type, title, body, linkUrl, entityId, ct)`** — the primitive.
  Persist, then push. Truncates to the column widths so a long subject name can't turn a
  successful order into a failed insert. Returns early on `Guid.Empty` (public-API orders
  have no ATS user).
- **`RaiseForOrderAsync(emailInvitationId, type, ct)`** — the one the callers use. Looks up
  the requestor and subject, builds the wording and the link, delegates to `RaiseAsync`.
  Keeps the four order-shaped events from each repeating that lookup.
- `GetNotificationsAsync` / `GetUnreadCountAsync` / `MarkAsReadAsync` / `MarkAllAsReadAsync`
  — read side, called by the handlers.

**No try/catch.** Both steps go through `SideEffectGuard` (see §7).

### Hub — `Hubs/IATSClient.cs`

Adds one method to the existing contract:

```csharp
Task ReceiveNotification(NotificationListDTO notification);
```

A **new** method, not an overload of `ReceiveATSResponse` — that one carries a plain string
that `NewOrderComponent` forwards to a snackbar, and it must keep working unchanged.

`ATSHub` itself is untouched. It already puts each connection into a group named after the
**validated token's** user id (`GetUserGroupName()` in
`BuildingBlocks/SignalR/HubCallerContextExtensions.cs`) — never a query-string value, which
was a real vulnerability once.

### Endpoints — `Features/Web/Notifications/`

Standard vertical slices: Carter endpoint → MediatR → handler → service.

| Route | Verb | Handler resolves recipient from |
|---|---|---|
| `/ats/getnotifications` | GET | `ICurrentUser.UserId` |
| `/ats/getunreadnotificationcount` | GET | `ICurrentUser.UserId` |
| `/ats/marknotificationread` | PATCH | `ICurrentUser.UserId` |
| `/ats/markallnotificationsread` | PATCH | `ICurrentUser.UserId` |

**The recipient always comes from the validated token, never the request.** There is no
parameter that can widen the scope. All four are registered in `Path/ATSPaths.cs` — the
gateway is part of the contract; a route absent there returns the SPA shell instead.

### Retention — `BackgroundJobs/Notifications/AtsNotificationRetentionService.cs`

`BackgroundService` on a `PeriodicTimer`, copied from `AtsAuditRetentionService`. Deletes in
batches via `ExecuteDeleteAsync` until a pass comes back short, so one sweep clears a
backlog without holding a giant DELETE open.

Tuned by `Configuration/AtsNotificationOptions.cs`, section `AtsNotifications`. Every value
has a working default, so **no appsettings change is required**:

| Setting | Default |
|---|---|
| `RetentionEnabled` | `true` |
| `RetentionDays` | `30` (the agreed one month) |
| `RetentionIntervalHours` | `24` |
| `RetentionBatchSize` | `5000` |

### Registration — `ServiceConfig/ATSServiceConfiguration.cs`

```csharp
services.AddScoped<IAtsNotificationRepository, AtsNotificationRepository>();
services.AddScoped<IAtsNotificationService, AtsNotificationService>();
services.AddHostedService<AtsNotificationRetentionService>();
services.Configure<AtsNotificationOptions>(configuration.GetSection(AtsNotificationOptions.SectionName));
```

---

## 4. Where notifications are raised

| Event | File | Placement |
|---|---|---|
| Candidate submitted the form | `Services/ApplicationForm/ApplicationFormService.cs` | after `CommitAsync` |
| Bulk upload **parsed** | `Services/BulkSubmissionProcessor/BulkSubmissionProcessorService.cs` | beside the existing `ReceiveATSResponse` toast, reusing `file.UploadedByUserId` |
| Bulk invitations **all emailed** | `Services/EmailNotificationProcessor/EmailNotificationProcessorService.cs` | after the sent/failed statuses are written |
| Report ready / order completed | `Services/Report/ReportService.cs` | after `CommitAsync`, on both the update and the insert path |
| Ticketing retries exhausted | `Services/OMSTicketing/OMSTicketingProcessorService.cs` | after each `MarkTicketFailedAsync` |

### Email sending throughput

Throughput is governed by a **process-wide send rate** rather than by a concurrency number,
and the details live in **`docs/ats-email-delivery.md`** — read that before changing
anything about how invitations are sent.

The short version, because it changes how this job behaves:

- Sends are paced by `SmtpRateLimiter` at `AtsEmailDeliveryOptions.MaxSendsPerSecond`
  (default **0.9/s**), over a small pool of **reused, pre-authenticated** SMTP sessions
  (default **2**). Raising the connection count does not raise the send rate.
- A provider throttle (SMTP 421/454) **stops the whole pass**. The remaining rows go back to
  `Pending` with their attempt count untouched, via `ReleaseEmailInvitationClaimsAsync`, and
  the next tick is skipped while the back-off is in force.
- A **permanent** rejection (5xx) fails the row on the first attempt instead of spending
  three. Only transient faults retry, with exponential back-off.
- **Each send still resolves `IEndorsementSubmissionService` from its own scope.** That
  service reaches a `DbContext` (it looks up the client name for the email body), and
  `DbContext` is **not thread-safe** — sharing one across concurrent sends corrupts its
  change tracker in ways that surface as unrelated errors much later.
  `BulkSubmissionProcessorService` does the same thing for the same reason.
- **Results collect into `ConcurrentBag`, not `List`.** `List<T>.Add` from several threads
  corrupts the backing array without throwing.
- **The 5s trigger is a poll interval, not a load multiplier.** The job is
  `[DisallowConcurrentExecution]`, so a trigger firing mid-pass is skipped entirely. It only
  decides how fast an *idle* worker notices new work — and it can no longer influence the
  send rate at all.

`StaleClaimTimeout` (30 min) must stay comfortably above a worst-case pass. At the default
rate that is about four minutes for a 200-row claim, plus up to ten more if a throttle
back-off elapses mid-pass.

**Bulk raises two notifications, at different times.** `BulkUploadCompleted` fires when the
file is parsed and the orders exist; `BulkEmailsCompleted` fires when every candidate has
actually been emailed ("All 40 of 40 invitation emails … have been sent"). The gap between
them can be minutes, and the second is the one a requestor is waiting on.

The subtlety is that **the email job sends in claimed slices, not whole files** — a
40-subject file may be sent across several passes. So `RaiseForCompletedBulkEmailsAsync` is
called after *every* pass, but `GetCompletedBulkEmailFilesAsync` asks the database which
files now have nothing left `Pending` or `Processing`. It therefore fires **once per file**,
not once per pass. Failures count as attempted: a file is finished when nothing is still in
flight, not when everything succeeded, and a partial result is worded
"37 of 40 … 3 could not be delivered" so it does not read as a success.

**The ticketing one is the subtle case.** A retryable failure only exhausts the budget on
its *last* attempt, so firing on every failure would notify five times for one order.
`NotifyIfTicketingExhaustedAsync` asks `GetExhaustedTicketIdsAsync` which ids are *actually*
spent — reading the attempt count back from the database, because only the database knows
what it became after the update.

---

## 5. Frontend classes

All under `UI/FrontendWebassembly/`.

### `DTO/ATS/NotificationDTO.cs`

Mirrors `NotificationListDTO`. **Keep the property names in step or the JSON stops
binding.**

### `Services/ATS/Notifications/NotificationService.cs`

Owns the hub connection, the badge, and the reads. Registered `AddScoped`, which in Blazor
WASM means **it lives as long as the app**.

| Member | Purpose |
|---|---|
| `UnreadCount` | The badge value. Seeded from the API, then kept current by the hub. |
| `NotificationsChanged` | `Action<NotificationDTO?>`. Non-null = a new arrival; null = the count moved. |
| `StartAsync()` | Idempotent. Seeds the count, opens the connection. |
| `GetNotificationsAsync(cursor, pageSize, unreadOnly)` | One keyset page. |
| `MarkAsReadAsync` / `MarkAllAsReadAsync` | Adjust `UnreadCount` locally rather than re-reading. |

**The credentialed handler is the part to understand:**

```csharp
.WithUrl(hubUrl, options =>
{
    options.HttpMessageHandlerFactory = inner => new CookieHandler { InnerHandler = inner };
})
```

The JWT lives in a `SameSite=Lax` cookie that the API reads from `Request.Cookies` only. A
bare `HubConnectionBuilder` does **not** send it cross-origin. Deployed that goes unnoticed
because the gateway serves the UI and the API from one origin — but in local development
they are `:5134` and `:5123`, so `Context.User` is null, the connection joins no group, and
nothing is ever delivered. `CookieHandler` sets `Include` + `Cors`, which is what the
handshake needs. **The same fix was applied to `EndorsementSubmissionService`, where it
repaired the bulk-upload toast that was silently dead in local dev.**

### `Component/ATS/Notifications/`

| File | What it is |
|---|---|
| `NotificationCenter.razor{,.cs,.css}` | The bell, badge and dropdown. Mounted once in `ATSLayout`, so the connection is opened once and stays live across ATS navigation. |

**Not every arrival toasts.** `NotificationCenter.ShouldToast` suppresses the snackbar for
`TicketingFailed` and `InvitationEmailFailed`. Those are raised per order by background
jobs, so a batch of 40 produced 40 toasts and buried the screen — they are the most likely
to arrive in bulk and the least likely to need acting on within the second, so the bell's
count is the right weight. Everything else is one-per-event by nature (a candidate submits
their own form; a bulk file finishes once), so a toast is proportionate. **All of them still
land in the bell** — this only decides what interrupts.
| `NotificationItem.razor{,.cs}` | One row. Shared by the dropdown and the page so they can't drift. Maps `Type` → icon + accent, and `CreatedAt` → "3m ago". |
| `NotificationsPage.razor{,.cs,.css}` | `/s&i/ats/notifications`. Infinite scroll. |
| `wwwroot/js/ats/notificationScroll.js` | `IntersectionObserver` that presses "Load more" when the sentinel scrolls into view. |

**Why the page is not a `TableComponent`:** a notification feed is chronological, not a
record set. There are no columns to sort and a pager would make you click to see what
happened five minutes ago. Cursor pagination + a sentinel gives the endless feed; the
"Load more" button inside the sentinel is the keyboard and no-JS path.

### The unsubscribe rule

Both components do this in `Dispose`:

```csharp
Notifications.NotificationsChanged -= OnNotificationsChanged;
```

**This is mandatory, not tidiness.** The service outlives every page, so without it each
visit leaves another subscription behind — one duplicate toast per visit, then none at all
once disposed components start throwing. This exact bug already shipped once; see the
comment in `NewOrderComponent.razor.cs`.

---

## 6. Click-through

`LinkUrl` is built server-side by `BuildOrderLink`. Every link is pre-filtered so the
reader lands on the row the notification is about, not the top of a list:

| Type | Link | Search term |
|---|---|---|
| Ticketing failed | `/s&i/ats/ticketingstatus?search=…` | **last name only** |
| Bulk upload | `/s&i/ats/bulkuploads?search=…` | file name |
| Everything else | `/s&i/ats/searchreport?search=…` | full name |

**The term differs per board because the boards search differently.** This is the trap:

```
Orders & Reports   ILIKE (FirstName || ' ' || LastName)   → full name matches
Ticketing Status   ILIKE FirstName OR ILIKE LastName      → full name matches NEITHER
Bulk Uploads       ILIKE FileName                         → file name matches
```

Sending a full name to Ticketing returns an empty board, which reads to the user as a
broken link. If you add a new destination, check its repository's search predicate before
choosing the term.

All three components take a `[SupplyParameterFromQuery(Name = "search")]` property and seed
`_searchString` in `OnInitializedAsync` — **before** the first server load, so the opening
page is already filtered rather than fetching everything then narrowing.

Before navigating, both components call `CanOpen()`, which maps the path back to a module
via `ModuleList` and checks it against the cascaded `ATSAccessibleModuleIds`. This is a
**courtesy** to avoid bouncing someone off `/access-denied` — the destination page still
runs its own `RequireATSModule` check. That is the real control.

---

## 7. No try/catch — how errors are handled

Per `docs/feature-development-guide.md`, feature code throws and lets
`CustomExceptionHandler` map it to a status code. Notifications are the documented
exception, and they use shared helpers rather than local blocks:

| Helper | Where | Why |
|---|---|---|
| `SideEffectGuard.RunAsync` | `BuildingBlocks/Exceptions/Handler/` | The caller has already committed. If a failed notification bubbled to `CustomExceptionHandler` it would return 500 — and the candidate would be told their submission failed when it actually saved. |
| `ApiRequestExtensions.SendAsync` | `UI/.../Services/Shared/Extensions/` | Owns the send / status-check / read-error / deserialize sequence once, so UI services read as the request they make. |
| `SafeJs` | same folder | JS interop during teardown throws when the circuit is already gone. Only swallows `JSDisconnectedException` and `ObjectDisposedException`. |

`SideEffectGuard` is narrow on purpose. Use it only when **all three** hold: the primary
work is committed, the user isn't waiting on the result, and losing it degrades the
experience rather than the data.

---

## 8. Styling

Colour comes entirely from the `--c-*` tokens in `wwwroot/css/theme.css`, so both themes
are covered by one set of rules — see `docs/ui-theming-and-responsiveness.md`. **No hex
literals.**

Row styles (`.ats-notif-row` and friends) live in `wwwroot/css/ats.css` rather than either
component's scoped sheet, because `NotificationItem` renders inside both scopes and the two
must not drift. Only what is unique to each screen is scoped.

At ≤600px the dropdown stops being a pinned 380px panel and becomes a near-full-width sheet
anchored to the viewport.

---

## 9. Verifying

```powershell
dotnet build 1CibiPlatform.sln
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS"
```

Then, with the app running:

1. **Headline case** — create an order, open the application-form link, submit it. The bell
   increments live, a toast appears, and clicking the item lands on Orders & Reports
   filtered to that subject.
2. **Bulk** — upload a file; a notification arrives on completion with the accepted/rejected
   counts.
3. **Offline** — log out, trigger an event, log back in. It is in the inbox and the badge is
   right. *This is what persistence buys and what a live-only design would fail.*
4. **Isolation** — two users, event for A only; B's bell must not move.
5. **Infinite scroll** — with >20 notifications, scroll the page; the next page loads
   without a click.
6. **Retention** — set `RetentionDays` low and confirm old rows sweep while recent ones
   survive.
7. Both themes at 390px and desktop.

`GET /__routes` on the gateway should list the four new routes.

---

## 10. Things a future change must not break

- **Persist before push.** Reversing it means a toast for something absent from the inbox,
  and a badge that disagrees with the list.
- **The recipient always comes from the token.** Any endpoint that takes a recipient id as
  a parameter is a way to read someone else's notifications.
- **Do not cache the repository.** A cached badge or first page defeats the point of a bell.
- **Do not overload `ReceiveATSResponse`.** It is a plain string consumed by
  `NewOrderComponent`.
- **Keep the unsubscribe in `Dispose`.** See §5.
- **Keep the two `AtsNotificationType` lists in step** (backend `Constants/`, frontend
  `ShareData/ATS/`).
- **Notify only on real ticketing exhaustion.** Firing on every failed attempt means five
  notifications for one order.
