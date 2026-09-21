# ATS In-App Notifications — Code Explanation

Companion to [`ats-notifications.md`](ats-notifications.md). That document explains *what* the bell
does and *why* persist-then-push is the design. This one exists so a developer can change the
implementation without opening every file cold — it walks the real call chains, names the exact
method at each hop, and quotes the code that carries the correctness.

Read it top to bottom once, then use it as a map: *"I'm changing X, what else touches it?"* is
answered by §11.

> **Read §0 first.** The design doc predates several later changes and is now wrong or incomplete
> in nine places, three of which are notification types that exist in the vocabulary and are never
> raised by anything. Every claim below was verified against the code on branch
> `feature/Update-ReadMe-File`; where the two disagree, this document follows the code.

---

## 0. Where the design doc no longer matches the code

| # | `ats-notifications.md` says | The code actually does |
|---|---|---|
| **C1** | §3 lists **seven** `AtsNotificationType` values | There are **ten**. `BulkEmailsCompleted`, `EmailAccountsExhausted` and `EmailAccountNeedsReverification` are all in `Constants/AtsNotificationType.cs` and all in the UI mirror, but none appears in the doc's constants list (§4 prose mentions only the first) |
| **C2** | §10 "Keep the two `AtsNotificationType` lists in step" | They **are** in step — ten for ten. The real gap is on the sender side: `OrderDisputed`, `InvitationEmailFailed` and `EmailAccountNeedsReverification` are **never raised by any code**. See §4.7 |
| **C3** | §4 lists **five** raise sites | There are **six**. `BulkEmailNotificationProcessorService.RaiseAccountsExhaustedAsync` fans `EmailAccountsExhausted` out to every ATS administrator, and is the only notification not addressed to an order's requestor |
| **C4** | §3 "The service … `RaiseAsync` … the primitive. `RaiseForOrderAsync` … the one the callers use" | Two callers use `RaiseAsync` **directly** (`BulkSubmissionProcessorService`, `BulkEmailNotificationProcessorService`), and there is a third entry point the doc never lists as a service member in §3: `RaiseForCompletedBulkEmailsAsync` |
| **C5** | §3's repository table | Omits `GetCompletedBulkEmailFilesAsync`, the three-query method that decides whether a bulk file is finished. It is described in §4 prose only |
| **C6** | §3 entity table: `Type` — "Stored as text", no width | `varchar(60)`, and **`Type` is the one field `Truncate` does not cover** (§2.4, §9.11) |
| **C7** | §5 "`StartAsync()` Idempotent." | It is not. The guard runs *after* an `await`, and a non-null-but-disconnected connection is replaced without being disposed. Two callers can each build a socket (§8.1, §9.3) |
| **C8** | §5 "The same fix was applied to `EndorsementSubmissionService`" | True — and **not** to `AtsAssistantService`, which still builds a bare connection *and* still passes `?userId=` on the URL: the exact parameter the security fix removed (§3.2, §9.5) |
| **C9** | §7 lists `SideEffectGuard` as the notification error path | `RaiseAccountsExhaustedAsync` hand-rolls its own `try/catch` instead, which `docs/feature-development-guide.md` explicitly forbids in feature code (§4.5, §9.10) |

Two things in the code that the design doc does not describe at all: the **route-access carve-out**
that lets `/s&i/ats/notifications` exist without being an ATS module (§8.7), and the fact that the
**admin roster used for the fan-out is the one HybridCache-cached read in the whole feature** (§7).

---

## 1. Persistence — the row and the table

### 1.1 Entity — `BackendAPI/Modules/ATS/Data/Entities/AtsNotification.cs`

A `sealed` POCO with no mapping attributes; everything is fluent (§1.2). The class doc states the
whole design in one sentence: *"The row is written first and pushed second, so the inbox is the
source of truth and the push is only an optimisation for someone who happens to be looking."*

```csharp
public sealed class AtsNotification
{
	public Guid NotificationId { get; set; }

	// The ATS user this is addressed to. Matches the SignalR group name produced by
	// HubCallerContextExtensions.GetUserGroupName, which is what makes the live push and
	// the stored row agree on the recipient.
	public Guid RecipientUserId { get; set; }

	// AtsNotificationType, stored as its string name rather than an int so a row stays
	// readable in the database and reordering the enum cannot silently retype history.
	public string Type { get; set; } = string.Empty;
```

That comment is the load-bearing invariant of the feature: **`RecipientUserId` and the SignalR
group name are the same string.** §10 lists it as one of the pairs nothing enforces at compile
time.

`LinkUrl` carries the reason it is nullable, and names the guard the UI adds on top:

```csharp
	// Where clicking the notification takes the user, as an app-relative path built by the
	// sender (for example "/s&i/ats/searchreport?search=Juan+Dela+Cruz"). Null when the
	// event has no screen worth opening. The UI still checks the recipient's module access
	// before rendering it as a link - see NotificationCenter.
	public string? LinkUrl { get; set; }

	// The order or bulk file the notification is about. Kept separately from LinkUrl so a
	// future screen can group or de-duplicate by subject without parsing a URL.
	public Guid? EntityId { get; set; }
```

There is **no `UpdatedAt`**, and no foreign key to `UserDetails` — the class doc gives the reason
(the row must survive the recipient being reassigned or deactivated), matching `AtsAuditEntry`.

### 1.2 EF configuration — `Data/EntityConfiguration/AtsNotificationConfiguration.cs`

Picked up by `modelBuilder.ApplyConfigurationsFromAssembly(typeof(ATSDBContext).Assembly);`
(`Data/Context/ATSDBContext.cs:37`); the `DbSet` is `public DbSet<AtsNotification> Notifications { get; set; }`
at line 30. Nothing is configured inline in the context.

```csharp
		builder.ToTable("Notifications", "ats");

		builder.HasKey(x => x.NotificationId);

		// Version 7 ids are minted in code so a row keeps the order it was raised in, and
		// so the id can be used as the keyset tie-breaker below.
		builder.Property(x => x.NotificationId)
			   .ValueGeneratedNever();
```

`ValueGeneratedNever()` is what forces `Guid.CreateVersion7()` in the service (§2.4) — the database
will not mint one, so a caller that forgets writes `Guid.Empty` and collides on the second row.

Column widths, all three of which the service truncates to:

```csharp
		builder.Property(x => x.Type)
			   .HasMaxLength(60)
			   .IsRequired();

		builder.Property(x => x.Title)
			   .HasMaxLength(160)
			   .IsRequired();

		// Wide enough for the bulk summary line, which interpolates a file name and two
		// counts. Senders truncate rather than letting an over-long body fail the write.
		builder.Property(x => x.Body)
			   .HasMaxLength(500)
			   .IsRequired();
```

Three indexes, each with its justification inline:

```csharp
		// Mirrors the inbox's fixed (CreatedAt DESC, NotificationId DESC) ordering within
		// one recipient, so a keyset page is an index scan rather than a sort.
		builder.HasIndex(x => new { x.RecipientUserId, x.CreatedAt, x.NotificationId })
			   .IsDescending(false, true, true);

		// The unread badge is read on every page load, so it gets its own narrow index
		// rather than riding the one above.
		builder.HasIndex(x => new { x.RecipientUserId, x.IsRead });

		// The retention sweep deletes the oldest rows first and needs ascending order.
		builder.HasIndex(x => x.CreatedAt);
```

Note `IsDescending(false, true, true)`: `RecipientUserId` is **ascending** because it is an equality
predicate, and only the two sort keys descend. `AtsNotificationRepository.ApplySeek` must mirror
this expression exactly — its own comment says so (§5.3).

### 1.3 Migration — `BackendAPI/API/APIs/Migrations/ATS/20260909034859_AddAtsNotificationsATSMigration.cs`

Namespace `APIs.Migrations.ATS`, per-module folder. Purely additive — one `CreateTable` and three
`CreateIndex` calls; `Down()` is a single `DropTable`. The index that carries the feed:

```csharp
            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_CreatedAt_NotificationId",
                schema: "ats",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "CreatedAt", "NotificationId" },
                descending: new[] { false, true, true });
```

Both timestamp columns are `timestamp with time zone`, so `DateTime.UtcNow` in the service is the
correct clock — a `DateTime.Now` here would be silently converted on write and read back shifted.

### 1.4 Vocabulary — `BackendAPI/Modules/ATS/Constants/AtsNotificationType.cs`

`public static class` of `public const string` — **not an enum**, for the stated reason:

```csharp
/// <remarks>
/// Strings rather than an enum for the same reason as OrderStatus and OrderHistoryEventType:
/// the value is persisted, so it has to stay readable in the database and survive someone
/// reordering the list. The UI maps each of these to an icon and an accent colour - adding
/// a value here without adding that mapping falls back to the neutral treatment rather
/// than breaking.
/// </remarks>
```

Unlike `TicketStatus` there is **no `All` array** — nothing validates a caller-supplied type, because
no endpoint accepts one. The ten values, and who raises each:

| Value | Raised by | § |
|---|---|---|
| `ApplicationFormSubmitted` | `ApplicationFormService` | 2 |
| `ReportReady` | `ReportService` (both paths) | 4.1 |
| `OrderCompleted` | `ReportService` (insert path, when the order completed) | 4.1 |
| `BulkUploadCompleted` | `BulkSubmissionProcessorService` | 4.2 |
| `BulkEmailsCompleted` | `AtsNotificationService.RaiseForCompletedBulkEmailsAsync` | 4.3 |
| `TicketingFailed` | `OMSTicketingProcessorService.NotifyIfTicketingExhaustedAsync` | 4.4 |
| `EmailAccountsExhausted` | `BulkEmailNotificationProcessorService.RaiseAccountsExhaustedAsync` | 4.5 |
| `OrderDisputed` | **nobody** | 4.7 |
| `InvitationEmailFailed` | **nobody** | 4.7 |
| `EmailAccountNeedsReverification` | **nobody** | 4.7 |

The two "pause versus failure" types are distinguished in the doc comment, and the UI reads that
distinction back out as colour:

```csharp
	/// <remarks>
	/// Separate from <see cref="EmailAccountsExhausted"/> because it is not self-clearing: a
	/// cap lifts on its own after 24 hours, a revoked app password never does. Sending both
	/// under one type would let the actionable one hide behind the transient one.
	/// </remarks>
	public const string EmailAccountNeedsReverification = "EmailAccountNeedsReverification";
```

The frontend mirror is `UI/FrontendWebassembly/ShareData/ATS/AtsNotificationTypes.cs`, consumed by
`NotificationItem.TypeIcon` and `NotificationItem.AccentModifier`. Both switches end in a fallback
rather than throwing:

```csharp
		// An unknown type is a server that knows about something this build does not.
		// Render it neutrally rather than dropping it.
		_ => Icons.Material.Filled.Notifications
```

---

## 2. End-to-end trace: "candidate submitted the application form"

The headline case, and the only one that runs through a request pipeline rather than a Quartz
thread. Follow it once and the rest are the same shape.

### 2.1 The caller — `Services/ApplicationForm/ApplicationFormService.cs`

`IAtsNotificationService` is a constructor dependency (field at line 12, parameter at line 44). The
call sits **after** `CommitAsync`, inside the same `try` whose `catch` compensates by deleting the
candidate's uploaded blobs:

```csharp
			await _unitOfWork.CommitAsync(ct);

			_logger.LogInformation("Succcessfully added the Application Form Data for {EmailId}: {@Context}", emailInvitationId, logContext);

			// After the commit, deliberately. The candidate's submission is the thing that
			// matters and it is now durable; telling the requestor is a best-effort follow-up
			// that must not be able to roll it back. RaiseForOrderAsync swallows its own
			// failures for the same reason.
			await _notificationService.RaiseForOrderAsync(
				emailInvitationId,
				AtsNotificationType.ApplicationFormSubmitted,
				ct);

			return true;
```

The safety of being inside that `try` rests entirely on `RaiseForOrderAsync` never throwing — which
is `SideEffectGuard`'s contract (§2.5), not a local guarantee. If a future edit let it throw, the
`catch` would delete a submitted candidate's files and report a failure for work that committed.

### 2.2 Resolving the recipient — `AtsNotificationService.RaiseForOrderAsync` (line 86)

This is the answer to *"how does a background thread with no `HttpContext` know who to tell"*: it
doesn't ask `ICurrentUser`, it reads the requestor back off the order.

```csharp
		// Same contract as RaiseAsync: the caller has already finished its real work, so a
		// failed lookup degrades to "no notification" rather than failing the request.
		var target = await SideEffectGuard.RunAsync(
			() => _notificationRepository.GetOrderTargetAsync(emailInvitationId, cancellationToken),
			_logger,
			$"resolve notification target for order {emailInvitationId}",
			cancellationToken: cancellationToken);

		// No order, or an order nobody in ATS placed (public API). Nothing to tell anyone.
		if (target?.RequestorId is null || target.RequestorId == Guid.Empty)
		{
			return;
		}

		var subjectName = string.IsNullOrWhiteSpace(target.SubjectName)
			? "A candidate"
			: target.SubjectName;
```

`GetOrderTargetAsync` is a four-column projection, kept on the *notification* repository on purpose:

```csharp
	public Task<NotificationOrderTargetDTO?> GetOrderTargetAsync(
		Guid emailInvitationId,
		CancellationToken cancellationToken) =>
		_dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(order => order.EmailInvitationID == emailInvitationId)
			.Select(order => new NotificationOrderTargetDTO
			{
				EmailInvitationId = order.EmailInvitationID,
				RequestorId = order.RequestorId,
				FirstName = order.FirstName,
				LastName = order.LastName
			})
			.FirstOrDefaultAsync(cancellationToken);
```

`SubjectName` is computed on the DTO, not stored, and drops null parts rather than emitting a
leading space:

```csharp
	/// <summary>The subject's display name, for the notification body and the deep link.</summary>
	public string SubjectName =>
		string.Join(' ', new[] { FirstName, LastName }
			.Where(part => !string.IsNullOrWhiteSpace(part)));
```

### 2.3 Wording and the deep link

`BuildOrderMessage` is a `switch` expression over the type, with a neutral default so an unmapped
type still produces a readable row:

```csharp
			AtsNotificationType.ApplicationFormSubmitted => (
				"Application form submitted",
				$"{subjectName} completed the application form you sent. The order is now in progress."),
```

```csharp
			_ => (
				"Order update",
				$"There is an update on the order for {subjectName}.")
```

`BuildOrderLink` is where the one non-obvious rule lives, and the comment is worth reading before
adding a destination:

```csharp
	// Ticketing failures belong on the ticketing board; everything else is an order, so it
	// opens Orders & Reports. Both are pre-filtered to the subject so the reader lands on
	// the row the notification is about rather than the top of a list.
	//
	// The two boards search differently, and the search term has to match the destination:
	//
	//   Orders & Reports  ILIKE over (FirstName || ' ' || LastName)  -> full name works
	//   Ticketing Status  ILIKE FirstName OR ILIKE LastName          -> full name matches
	//                                                                  NEITHER column
	//
	// So ticketing gets the last name alone. Sending "Russel Gutierrez" there returns an
	// empty board, which reads as a broken link.
```

```csharp
		if (type == AtsNotificationType.TicketingFailed)
		{
			return string.IsNullOrWhiteSpace(lastName)
				? "/s&i/ats/ticketingstatus"
				: $"/s&i/ats/ticketingstatus?search={Uri.EscapeDataString(lastName)}";
		}

		return string.IsNullOrWhiteSpace(subjectName)
			? "/s&i/ats/searchreport"
			: $"/s&i/ats/searchreport?search={Uri.EscapeDataString(subjectName)}";
```

Both branches degrade to the unfiltered board rather than to `null`, so the row stays clickable.
`BulkUploadCompleted` and `EmailAccountsExhausted` build their links at the call site instead
(§4.3, §4.5) because neither is about an order.

### 2.4 Persist, then push — `AtsNotificationService.RaiseAsync`

The primitive. Three things happen in a fixed order and the order is the design:

```csharp
		// No recipient means nobody to tell. Happens for orders placed through the public
		// API, which have no ATS user behind them.
		if (recipientUserId == Guid.Empty)
		{
			return;
		}

		var notification = new AtsNotification
		{
			// Version 7 so rows sort by creation even when two land in the same tick, which
			// is what makes the keyset tie-breaker stable.
			NotificationId = Guid.CreateVersion7(),
			RecipientUserId = recipientUserId,
			Type = type,
			Title = Truncate(title, TitleMaxLength) ?? string.Empty,
			Body = Truncate(body, BodyMaxLength) ?? string.Empty,
			LinkUrl = Truncate(linkUrl, LinkUrlMaxLength),
			EntityId = entityId,
			IsRead = false,
			CreatedAt = DateTime.UtcNow
		};
```

Note what is *not* truncated: `Type`. See §9.11.

```csharp
		// Persist first, push second. A push reaches only a live connection; the row is
		// what makes the notification survive the recipient being logged out, and it is
		// also what the badge counts.
		//
		// Both steps go through SideEffectGuard rather than being allowed to throw: every
		// caller is finishing work that has already committed, so letting an exception
		// reach CustomExceptionHandler would answer a successful submission with a 500.
		var persisted = false;

		await SideEffectGuard.RunAsync(
			async () =>
			{
				await _notificationRepository.AddAsync(notification, cancellationToken);
				persisted = true;
			},
			_logger,
			$"persist notification {type} for {recipientUserId}",
			cancellationToken);

		// Nothing stored means nothing to announce - the recipient would see a toast for a
		// notification that is not in their inbox.
		if (!persisted)
		{
			return;
		}

		await SideEffectGuard.RunAsync(
			() => _hubContext
				.Clients
				.Group(recipientUserId.ToString())
				.ReceiveNotification(ToDto(notification)),
			_logger,
			$"push notification {notification.NotificationId}",
			cancellationToken);
```

`persisted` is captured by the lambda rather than inferred from a return value because the
`Func<Task>` overload of `SideEffectGuard.RunAsync` has nothing to return. The flag is the only
thing that makes "stored but not pushed" and "not stored at all" distinguishable.

`.Group(recipientUserId.ToString())` is default `Guid.ToString()` ("D", lowercase, hyphenated) —
exactly what `GetUserGroupName` produces on the join side (§3.3). Two independent `ToString()`
calls with nothing tying them together; see §10.

`ToDto` projects the entity onto `NotificationListDTO` **without** `RecipientUserId`, matching the
DTO's own remark: *"the caller can only ever read their own notifications, so echoing the id back
adds nothing and invites it being trusted."*

The truncation helper is the column widths expressed as code:

```csharp
	private const int TitleMaxLength = 160;
	private const int BodyMaxLength = 500;
	private const int LinkUrlMaxLength = 500;

	// The column widths are the contract. A subject with an unusually long name must not
	// turn a successful order into a failed insert.
	private static string? Truncate(string? value, int maxLength) =>
		string.IsNullOrEmpty(value) || value.Length <= maxLength
			? value
			: value[..maxLength];
```

Three constants that must be edited in the same commit as `AtsNotificationConfiguration` — nothing
derives one from the other.

### 2.5 `SideEffectGuard` — why the notification cannot throw

`BuildingBlocks/Exceptions/Handler/SideEffectGuard.cs`. The class doc names this feature as its
reason for existing:

```csharp
/// A small number of operations are the opposite case. Raising a notification after a
/// candidate submits an application form, or after a bulk file finishes parsing, is a
/// follow-up to work that has already committed. If that follow-up throws and is allowed
/// to bubble, CustomExceptionHandler does exactly its job and returns a 500 - and the
/// candidate is told their submission failed when it actually succeeded, so they submit
/// again. The exception has to stop before it reaches the pipeline.
```

Both overloads distinguish shutdown from fault:

```csharp
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Shutdown or a cancelled request, not a fault. The primary work is already
			// committed, so there is nothing to report and nothing to undo.
		}
		catch (Exception exception)
		{
			logger.LogError(
				exception,
				"Best-effort side effect failed and was suppressed: {Description}",
				description);
		}
```

The value-returning overload takes a `fallback`, which `RaiseForCompletedBulkEmailsAsync` uses to
degrade to an empty list rather than to a throw (§4.4).

### 2.6 The push crosses to the browser

`Clients.Group(...).ReceiveNotification(dto)` is strongly typed through `IHubContext<ATSHub, IATSClient>`,
so the server side cannot misspell the method. The client side can — it subscribes by string
(§8.1). Delivery lands in `NotificationService`:

```csharp
		_hubConnection.On<NotificationDTO>("ReceiveNotification", notification =>
		{
			UnreadCount++;
			NotificationsChanged?.Invoke(notification);
		});
```

and fans out to whichever components are mounted:

```csharp
			await InvokeAsync(() =>
			{
				if (arrived is not null)
				{
					// Keep the open dropdown honest without a refetch.
					_items.Insert(0, arrived);
```

(`NotificationCenter.razor.cs`, `OnNotificationsChangedAsync`.) The non-null/null split on the event
argument is the protocol: **non-null = a new arrival, null = the count moved** (§8.1).

---

## 3. The hub — group naming, the guard, and how it is reached

### 3.1 `IATSClient` — one added method, in the same file as the hub

`BackendAPI/Modules/ATS/Hubs/IATSClient.cs` holds **both** the contract and `ATSHub`; there is no
`ATSHub.cs`. The contract before the addition:

```csharp
public interface IATSClient
{
	Task ReceiveATSResponse(string message);
	Task ReceiveChatResponse(string message);
	Task ReceiveChatTyping(bool isTyping);
	Task SessionCleared();
```

and the notification method, with the reason it is not an overload:

```csharp
	/// <remarks>
	/// Deliberately its own method rather than another ReceiveATSResponse: that one carries
	/// an unstructured string a client can only forward to a snackbar, and NewOrderComponent
	/// depends on exactly that behaviour. A notification needs an id, a type and a link, so
	/// it gets a typed payload - the same DTO the inbox endpoint returns, so the client has
	/// one code path for a live arrival and a fetched row.
	/// </remarks>
	Task ReceiveNotification(NotificationListDTO notification);
```

There are **no client-to-server hub methods** on `ATSHub` — the hub is a pure delivery channel. A
connected client can receive but cannot invoke anything.

### 3.2 The current guard, verbatim

This is the fix for `docs/reviews/ats-oneplatform-fix-details.md` item 2. The old hub read
`Request.Query["userId"]`; the current one reads only the principal:

```csharp
public class ATSHub : Hub<IATSClient>
{
	public override async Task OnConnectedAsync()
	{
		var userId = Context.GetUserGroupName();

		if (!string.IsNullOrWhiteSpace(userId))
		{
			await Groups.AddToGroupAsync(Context.ConnectionId, userId);
		}

		await base.OnConnectedAsync();
	}

	// SignalR removes a connection from its groups when it disconnects, so there is
	// nothing to undo on the way out.
}
```

Three properties of this guard, all deliberate:

1. **`Context.User`, never `Context.GetHttpContext()?.Request`.** Nothing in the method can be
   influenced by the caller. The `?userId=` in the URL is now inert — which is why §9.5 flags the
   one client that still sends it.
2. **`await`ed.** The review records that `AIAgentHub` originally dropped it, so a client could be
   sent its first message before it finished joining.
3. **No `OnDisconnectedAsync` at all.** Group membership is connection-scoped on the server; the
   override was removed rather than left as a mirror image that could drift.

The class doc is where the *absence* of `[Authorize]` is justified, and it is worth quoting in full
because it is the single most likely thing for a reviewer to "fix" and break:

```csharp
/// <remarks>
/// The group name is taken from the authenticated principal, never from the query
/// string. Reading a caller-supplied <c>?userId=</c> here let anyone join anyone else's
/// group and receive their candidate data; deriving it from <c>Context.User</c> is what
/// closes that - a connection with no principal joins no group and receives nothing.
///
/// Deliberately NOT [Authorize]: the client builds this connection with a bare
/// HubConnectionBuilder, which does not go through CookieHandler and so does not set
/// BrowserRequestCredentials.Include. The UI and the API are different origins, so the auth
/// cookie is not sent on the handshake and [Authorize] would reject every connection
/// with a 401. Adding it back requires giving the connection a credentialed handler
/// first - see AIChatService/EndorsementSubmissionService.
/// </remarks>
```

The review doc's §"Correction: `[Authorize]` was added, then removed" records that this was tried
and reverted: `NewOrderComponent.OnInitializedAsync` awaits `StartAsync()`, so the 401 took the
whole page down, and no test caught it because `UseSignalRConfiguration` early-returns under the
`Testing` environment (§3.5).

### 3.3 `GetUserGroupName` — `BuildingBlocks/SignalR/HubCallerContextExtensions.cs`

Shared by `ATSHub` and `AIAgentHub` because both had the same bug:

```csharp
	// The same pair CurrentUser reads, in the same order, so a hub group and an
	// ICurrentUser.UserId always agree on who the caller is.
	private const string UserIdClaim = "userId";
```

```csharp
	public static string? GetUserGroupName(this HubCallerContext context)
	{
		var value = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

		if (string.IsNullOrWhiteSpace(value))
			value = context.User?.FindFirst(UserIdClaim)?.Value;

		// Round-trip through Guid so the group name is canonically formatted regardless
		// of how the claim was written.
		return Guid.TryParse(value, out var userId) && userId != Guid.Empty
			? userId.ToString()
			: null;
	}
```

The `Guid.TryParse` round-trip does two jobs at once: it rejects a claim that is not a GUID (so a
crafted token cannot pick an arbitrary group name such as another user's email), and it normalises
the format so a claim written as `N` or `B` still lands in the same group the service pushes to.
`Guid.Empty` is rejected explicitly — that is the same sentinel `RaiseAsync` returns early on, so
neither side can address a "nobody" group.

The helper reads `Context.User` rather than injecting `ICurrentUser` for a reason the review doc
spells out: `CurrentUser` is `IHttpContextAccessor`-backed and `HttpContext` is null for hub
*invocations* after the handshake, so it would intermittently return null inside a hub.

### 3.4 Mapping and the gateway route

`BackendAPI/API/APIs/ServiceConfig/AppConfiguration.cs`:

```csharp
	public static WebApplication UseSignalRConfiguration(
		this WebApplication app,
		IConfiguration configuration)
	{
		if (app.Environment.IsEnvironment("Testing"))
		{
			return app;
		}

		app.MapHub<AIAgentHub>(configuration["SignalRHub:Endpoint"]!);
		app.MapHub<ATSHub>(configuration["SignalRHub:ATSBulkEndpoint"]!);
		app.UseWebSockets();
		return app;
	}
```

**The notifications hub does not have its own route — it shares `/hubs/atsbulk` with bulk-upload
progress and the AI assistant.** One hub, one endpoint, three payloads distinguished only by method
name. The config key has no literal anywhere in the repository: every appsettings file carries the
placeholder `"ATSBulkEndpoint": "${SIGNALRHUB__ATSBULKENDPOINT}"`, so the actual path is supplied by
the environment and must equal the gateway's `MatchPath` for the route to work at all.

The `Testing` early-return is why `AtsHubGroupIsolationTests` builds its own host (§3.6).

The gateway entry, `BackendAPI/Modules/ATS/Path/ATSPaths.cs:651-657`:

```csharp
			new RouteDefinitionDTO(
				RouteId: "GetBulkInsertResponseEntryPoint",
				MatchPath: "/hubs/atsbulk/{**catch-all}",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get, GatewayConstants.HttpMethod.Post}
			),
```

Two things to notice. **There is no `Transforms` entry** — unlike every `/ats/*` route, this one
forwards the path unchanged, which is precisely why `SignalRHub:ATSBulkEndpoint` must be
`/hubs/atsbulk` and not something else. And `GET` **and** `POST` are both allowed, because a SignalR
WebSocket handshake is a GET while the long-polling fallback POSTs to the same path; the
`{**catch-all}` covers `negotiate`.

There is **no `RateLimitPolicy` metadata** on this route (§9.6).

### 3.5 Route id versus purpose

The `RouteId` is still `GetBulkInsertResponseEntryPoint` — it predates notifications and the AI
assistant. Grepping the gateway for "notification" will not find the hub route. Rename it only with
the understanding that route ids surface in `GET /__routes` and in YARP metrics.

### 3.6 `AtsHubGroupIsolationTests` — what it pins

`Test/Test/BackendAPI/Modules/ATS.IntegrationTests/AtsHubGroupIsolationTests.cs`. The class doc
explains why it needs its own host:

```csharp
/// <remarks>
/// These exist because the original hub took its group name from
/// <c>Request.Query["userId"]</c>, so connecting with somebody else's GUID delivered
/// their bulk-upload notifications and AI-assistant replies. Nothing caught that: the
/// API host skips <c>MapHub</c> entirely under the "Testing" environment, so the shared
/// IntegrationTestWebAppFactory never exercises a hub.
///
/// So this spins up a minimal host with only the hub and a stub authentication handler.
/// No database, no Testcontainer - the subject under test is the group-assignment rule
/// in OnConnectedAsync, and nothing else needs to be real for that.
/// </remarks>
```

Five facts, each pinning one property of the guard:

| Fact | Pins |
|---|---|
| `Connection_ShouldReceiveMessagesForItsOwnUser` | The happy path — the guard did not over-correct into delivering nothing |
| `Connection_ShouldNotReceiveAnotherUsersMessages` | Cross-user isolation between two authenticated connections |
| `Connection_ShouldIgnoreAUserIdSuppliedInTheQueryString` | **The regression test for item 2.** Connects with `?userId=<victim>` and asserts the attacker's list stays empty |
| `Connection_WithNoIdentity_ShouldJoinNoGroupAndReceiveNothing` | The anonymous case, i.e. that the missing `[Authorize]` is actually covered by the group rule |
| `TwoConnectionsForTheSameUser_ShouldBothJoinThatUsersGroup` | Multi-tab: both sockets land in the same group, and cannot land anywhere else |

The identity is injected rather than negotiated, and the stub emits the same claim the real JWT
pipeline does:

```csharp
			var identity = new ClaimsIdentity(
				[new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
				SchemeName);
```

Negative assertions are only made after a positive one on the *same* send has landed, which is what
makes `attackerReceived.Should().BeEmpty()` meaningful rather than a race:

```csharp
		// Wait on the victim, who should get it - if the attacker were also going to
		// receive it, it would have arrived by now.
		await WaitForAsync(() => victimReceived.Count > 0);
```

**What these tests do not cover.** Every assertion goes through
`CaptureAtsResponses`, which subscribes to `nameof(IATSClient.ReceiveATSResponse)` — a plain string.
They pin the *group-assignment rule*, which is method-agnostic, but no test asserts that a
`ReceiveNotification` payload reaches the right client end to end. `AtsNotificationServiceTests`
covers the server half with a mocked `IHubContext`; the two suites meet in the middle and never
overlap.

One case was dropped deliberately, with the reason recorded in the file:

```csharp
	// Deliberately not tested here: reconnect-after-stop. Under TestServer with the
	// LongPolling transport, a connection that has been stopped stops group deliveries
	// to *subsequent* connections in the same host - two concurrently live connections
	// work fine, so it is a harness artifact rather than hub behaviour. Asserting
	// through it would test the transport, not ATSHub.
```

---

## 4. The other five write paths

Same shape as §2 — resolve a recipient from persisted data, build wording and a link, call
`RaiseAsync` — so only the differences are given.

### 4.1 `ReportService` — one call site, two types

`Services/Report/ReportService.cs`, lines 116-121 (update path) and 150-160 (insert path). Both
sit after `CommitAsync`. The insert path picks the type from the order's new status:

```csharp
			if (added)
			{
				// Completed is the terminal state the requestor is waiting for, so it gets
				// the stronger wording; anything else is "a report is ready to read".
				await _notificationService.RaiseForOrderAsync(
					invitation.EmailInvitationID,
					orderStatus == OrderStatus.Completed
						? AtsNotificationType.OrderCompleted
						: AtsNotificationType.ReportReady,
					cancellationToken);
			}
```

`added` gates it, so a failed insert does not announce a report. Both paths are inside a `try`
whose `catch` rolls back and deletes the uploaded blob — same dependence on never-throwing as §2.1.

### 4.2 `BulkSubmissionProcessorService` — calls `RaiseAsync` directly

`Services/BulkSubmissionProcessor/BulkSubmissionProcessorService.cs:266-292`. Not an order, so
`RaiseForOrderAsync` does not apply. Runs on a Quartz thread, so the recipient comes off the file
row. It sits directly beneath the pre-existing toast, and the comment states the relationship:

```csharp
				await _hubContext
						.Clients
						.Group(file.UploadedByUserId.ToString()!)
						.ReceiveATSResponse(uploadMessage);

				// The toast above only reaches an uploader who still has the app open on
				// the page that listens for it. This is the durable half: it survives a
				// refresh, and it is there on Monday for a file that finished on Friday.
				if (file.UploadedByUserId is Guid uploaderId)
				{
					var notificationService = scope.ServiceProvider
						.GetRequiredService<IAtsNotificationService>();
```

Resolved from the **per-file scope**, not injected — the processor fans files out under a
`SemaphoreSlim`, and a scoped service holding a `DbContext` cannot be shared across them.

The link is built here rather than by `BuildOrderLink`, because the bulk board searches on file
name:

```csharp
					// Pre-filtered to the file name, which is what the bulk board's search
					// matches on, so the uploader lands on their file rather than the top
					// of the list.
					var bulkLink = string.IsNullOrWhiteSpace(file.FileName)
						? "/s&i/ats/bulkuploads"
						: $"/s&i/ats/bulkuploads?search={Uri.EscapeDataString(file.FileName)}";
```

Note the asymmetry two lines apart: the toast targets
`.Group(file.UploadedByUserId.ToString()!)` with a null-forgiving operator, where a null
`UploadedByUserId` yields the empty string and a group nobody is in. The notification immediately
below guards properly with `is Guid uploaderId`. Only one of the two handles public-API uploads.

### 4.3 `RaiseForCompletedBulkEmailsAsync` — once per file, not once per pass

Called from `BulkEmailNotificationProcessorService` line 201, after the sent/failed statuses are
written so the completeness check reads this pass's outcome:

```csharp
		// After the statuses are written, so the completeness check reads the outcome of
		// this pass rather than the state before it. Deferred rows are excluded on purpose:
		// they are still in flight, and a file is only finished when nothing remains.
		var attempted = successList
			.Concat(errorList)
			.Select(request => request.EmailInvitationID)
			.ToList();

		await _notificationService.RaiseForCompletedBulkEmailsAsync(attempted, cancellationToken);
```

The service method uses the `fallback` overload of `SideEffectGuard` so a failed lookup degrades to
"no files finished" rather than to a throw, and iterates whatever came back:

```csharp
		var completedFiles = await SideEffectGuard.RunAsync(
			() => _notificationRepository.GetCompletedBulkEmailFilesAsync(
				sentEmailInvitationIds,
				cancellationToken),
			_logger,
			"resolve bulk files whose invitation emails just completed",
			fallback: [],
			cancellationToken);

		foreach (var file in completedFiles ?? [])
		{
			if (file.UploadedByUserId is not Guid uploaderId)
			{
				// Uploaded through the public API - no ATS user to tell.
				continue;
			}
```

The wording branches on whether anything failed, so a partial result never reads as a success:

```csharp
			// The counts are the message. "40/40" is what the uploader is waiting to see;
			// a partial result names the failures so they can be resent.
			var body = file.FailedCount == 0
				? $"All {file.SentCount} of {file.TotalCount} invitation emails for {fileLabel} have been sent."
				: $"{file.SentCount} of {file.TotalCount} invitation emails for {fileLabel} were sent. {file.FailedCount} could not be delivered and can be resent.";
```

**The repository method is what makes "once per file" true.** `GetCompletedBulkEmailFilesAsync` is
three queries, and the middle one is the whole rule:

```csharp
		// Counted across the whole file, not the batch: the job sends in claimed slices, so
		// a file is only finished when none of its orders are still Pending or Processing.
		var progress = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(order => order.BulkFileID != null && fileIds.Contains(order.BulkFileID.Value))
			.GroupBy(order => order.BulkFileID!.Value)
			.Select(group => new
			{
				FileId = group.Key,
				TotalCount = group.Count(),
				SentCount = group.Count(order => order.EmailSentStatus == EmailStatus.Done),
				FailedCount = group.Count(order => order.EmailSentStatus == EmailStatus.Error),
				InFlightCount = group.Count(order =>
					order.EmailSentStatus == EmailStatus.Pending
					|| order.EmailSentStatus == EmailStatus.Processing)
			})
			.Where(file => file.InFlightCount == 0)
			.ToListAsync(cancellationToken);
```

`Where(file => file.InFlightCount == 0)` is translated into a `HAVING` clause, so completeness is
decided by the database over the whole file rather than by counting the batch in memory. The first
query excludes single orders (`order.BulkFileID != null`) because they were already notified at
creation; the third joins back to `BulkUploadFileDetails` for the file name and uploader.

There is **no "already notified" flag**, so the guarantee rests entirely on `InFlightCount == 0`
becoming true exactly once. It can become true again: `ReleaseEmailInvitationClaimsAsync` puts
throttled rows back to `Pending`, and a resend flow can move a row off `Done`. Either re-opens a
finished file and the next pass that closes it raises a second `BulkEmailsCompleted`.

### 4.4 `OMSTicketingProcessorService.NotifyIfTicketingExhaustedAsync` — the concrete background example

This is the case the task asks about, and it is the subtlest of the six. Full method
(`Services/OMSTicketing/OMSTicketingProcessorService.cs:108-137`):

```csharp
	/// <summary>
	/// Tells the requestor when an order's automatic ticketing retries are spent, so it is
	/// not left sitting in Error until somebody happens to open the ticketing board.
	/// </summary>
	/// <remarks>
	/// Asks the repository which of the ids are actually exhausted rather than assuming:
	/// a retryable failure only exhausts the budget on its last attempt, and firing on
	/// each one would notify five times for the same order.
	/// </remarks>
	private static async Task NotifyIfTicketingExhaustedAsync(
		IServiceScope scope,
		IOMSTicketingRepository repository,
		Guid emailInvitationId,
		CancellationToken cancellationToken)
	{
		var exhausted = await repository.GetExhaustedTicketIdsAsync(
			[emailInvitationId],
			cancellationToken);

		if (exhausted.Count == 0)
		{
			return;
		}

		var notificationService = scope.ServiceProvider.GetRequiredService<IAtsNotificationService>();

		await notificationService.RaiseForOrderAsync(
			emailInvitationId,
			AtsNotificationType.TicketingFailed,
			cancellationToken);
	}
```

How the recipient is attributed with no `HttpContext`, in three hops:

1. The method is `static` and takes the **caller's already-open `IServiceScope`** — it does not
   create one, because `ProcessOneAsync` opened it for the `DbContext` and the notification
   repository must share that same scope.
2. `RaiseForOrderAsync` reads `RequestorId` off the `EmailInvitationRequest` row (§2.2), which
   `BulkSubmissionProcessorService` populated from `file.UploadedByUserId` and
   `InsertEmailInvitationRequestAsync` from `_currentUser.UserId` **at enrolment time**. The
   identity was persisted while there *was* an HTTP context; the job only reads it back.
3. `IAtsNotificationService` takes the recipient as a parameter on every write method. Its
   interface doc states the rule: *"every method takes the recipient explicitly rather than
   resolving it from `ICurrentUser` - a Quartz job has no HTTP context to resolve one from."*

Three call sites, one per failure mode, all immediately after a `MarkTicketFailedAsync`:

| Line | Failure | `isRetryable` |
|---|---|---|
| 183 | Payload could not be mapped onto an OMS ticket — parked before any OMS call | `false` |
| 231 | `BadRequestException` — OMS rejected it on business grounds | `false` |
| 256 | Anything else — treated as transient and retried | `true` |

Only the third can fire five times, which is exactly what the read-back prevents. The repository
method explains why it cannot be inferred:

```csharp
		// Read back rather than inferring from the update above: only the database knows
		// what TicketAttempts became, and the notification must fire exactly once - when
		// the budget runs out - not on every transient failure along the way.
		return await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(x => ids.Contains(x.EmailInvitationID)
				&& x.TicketStatus == TicketStatus.Error
				&& !x.IsTicketed
				&& x.TicketAttempts >= MaxTicketAttempts)
			.Select(x => x.EmailInvitationID)
			.ToListAsync(cancellationToken);
```

For the two non-retryable paths the guard is a formality — `MarkTicketFailedAsync` sets
`TicketAttempts = MaxTicketAttempts` outright when `isRetryable` is false, so the read-back always
comes positive and the notification always fires on the first attempt.

### 4.5 `BulkEmailNotificationProcessorService.RaiseAccountsExhaustedAsync` — the fan-out

The only notification not addressed to an order's requestor, and the only one with more than one
recipient. Two entry points into it, both in `ProcessForPendingStatusAsync`:

```csharp
		// Claiming rows when there is genuinely nowhere to send would only park them behind the
		// back-off while holding them out of every other worker's reach; leaving them Pending
		// costs one idle tick and nothing else.
		if (!await HasSendableAccountAsync(cancellationToken))
		{
			_logger.LogWarning(
				"Skipping email pass: every registered sender account is capped, cooling down, unverified or disabled.");

			await RaiseAccountsExhaustedAsync(cancellationToken);

			return;
		}
```

and, later in the same pass, when rows were abandoned mid-flight:

```csharp
			// Raised here rather than inside the send, where it would fire once per abandoned
			// row and bury the bell under hundreds of identical entries for one outage.
			await RaiseAccountsExhaustedAsync(cancellationToken);
```

That comment is right about *within* one pass. It says nothing about *across* passes — see §9.1.

The method itself:

```csharp
	private async Task RaiseAccountsExhaustedAsync(CancellationToken cancellationToken)
	{
		try
		{
			var recipients = await _repository.GetAtsAdministratorUserIdsAsync(cancellationToken);

			if (recipients.Count == 0)
			{
				_logger.LogWarning(
					"Every sender account is unavailable, but no ATS administrator could be found to notify.");

				return;
			}

			foreach (var recipient in recipients)
			{
				await _notificationService.RaiseAsync(
					recipient,
					AtsNotificationType.EmailAccountsExhausted,
					"Invitation emails have stopped",
					"Every registered sender account is capped, cooling down or unverified. Invitations are being held and will resume automatically once an account recovers.",
					"/s&i/ats/emailaccounts",
					null,
					cancellationToken);
			}
		}
		catch (Exception exception)
		{
			_logger.LogError(
				exception,
				"Could not raise the exhausted sender accounts notification.");
		}
	}
```

Four things to note. `EntityId` is `null` — there is no subject. The link is a bare module path with
no `?search=`, because there is nothing to filter to. Each recipient gets their **own row** (a
sequential `foreach`, so N admins = N inserts and N pushes, each to a single-user group). And the
`try/catch` is hand-rolled where every other notification path uses `SideEffectGuard` (§9.10).

The roster query is the reason `Distinct()` matters:

```csharp
	// Distinct is load-bearing, not tidiness: UserDetails has one row per user PER MODULE, so
	// without it an admin holding twelve modules is notified twelve times about one outage.
	public async Task<IReadOnlyList<Guid>> GetAtsAdministratorUserIdsAsync(CancellationToken cancellationToken) =>
		await _dbcontext.UserDetails.AsNoTracking()
			.Where(user => user.IsActive
				&& user.Role.IsActive
				&& (user.RoleId == AtsRoleIds.PlatformManager || user.RoleId == AtsRoleIds.Admin))
			.Select(user => user.UserId)
			.Distinct()
			.ToListAsync(cancellationToken);
```

(`Data/Repository/Users/ATSRepository.Users.cs:120`.) Both `IsActive` checks are present, so a
deactivated admin or a retired role drops out of the fan-out automatically.

### 4.6 What is *not* a write path

`AtsAssistantService` (`Services/AIAssistant/`) injects `IHubContext<ATSHub, IATSClient>` but never
touches `IAtsNotificationService` — the assistant streams through `ReceiveChatResponse` and is not
persisted. Likewise nothing outside `AtsNotificationService` writes to `ats.Notifications`; a grep
for `_dbContext.Notifications` / `Notifications.Add` finds only `AtsNotificationRepository` and the
retention sweep.

### 4.7 Three types nothing raises

`OrderDisputed`, `InvitationEmailFailed` and `EmailAccountNeedsReverification` have wording in
`BuildOrderMessage` (the first two), an icon and an accent in `NotificationItem`, and a mirror
constant in the UI — and no producer. Consequences worth knowing before you assume they work:

- `NotificationCenter.ShouldToast` suppresses toasts for `InvitationEmailFailed`. That rule is
  currently unreachable.
- `EmailAccountNeedsReverification` is documented as the actionable counterpart to
  `EmailAccountsExhausted` — the one that does not clear itself. Today only the self-clearing one is
  ever sent, so an admin sees "will resume automatically" for an outage that will not.
- A dispute or an undeliverable invitation is silent. `DisputeOrderService` raises no notification;
  it sends an email.

These are placeholders, not bugs — but they read as live behaviour from the vocabulary alone.

---

## 5. The read path

Four slices under `Features/Web/Notifications/`, each in its own operation folder with its own
`*Endpoint.cs` and `*Handler.cs`. All four are `RequireAuthorization()` with **no** ATS module or
permission policy — an inbox is not a permissioned module (§8.7).

### 5.1 Traced in full: `GET getnotifications`

`Query/GetNotifications/GetNotificationsEndpoint.cs`:

```csharp
public record GetNotificationsEndpointRequest(
	string? Cursor = null,
	int? PageSize = 15,
	bool UnreadOnly = false);

public record GetNotificationsEndpointResponse(KeysetPaginatedResult<NotificationListDTO> Notifications);
```

```csharp
		app.MapGet("getnotifications", async (
			[AsParameters] GetNotificationsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetNotificationsQueryRequest(
				request.Cursor,
				request.PageSize,
				request.UnreadOnly);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetNotificationsEndpointResponse(result.Notifications));
		})
```

`[AsParameters]` binds the three values from the query string. The `.WithDescription` states the
security property in the OpenAPI document itself:

```csharp
		.WithDescription(
			"Retrieves the caller's own in-app notifications, newest first, with keyset "
			+ "pagination for the infinite-scroll notifications page. The recipient is "
			+ "always the authenticated user - there is no parameter to read another "
			+ "user's inbox.")
```

The validator is where the "bad cursor is not an error" rule is written down:

```csharp
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");

		// Cursor is deliberately unvalidated: cursors are opaque and a malformed one
		// self-heals to the first page rather than failing the request.
```

The handler resolves the recipient and fails closed to an empty page rather than throwing:

```csharp
		// The recipient comes from the validated token, never the request. This is the
		// whole isolation story for the inbox.
		var recipientUserId = _currentUser.UserId;

		if (recipientUserId is null || recipientUserId == Guid.Empty)
		{
			return new GetNotificationsQueryResult(
				new KeysetPaginatedResult<NotificationListDTO>(
					Array.Empty<NotificationListDTO>(),
					null,
					0));
		}
```

That early return matters: a token issued before the `userId` claim existed yields an empty inbox
rather than a 500, and — critically — **never** an unfiltered query. There is no code path in which
`GetNotificationsAsync` is called without a recipient id.

The service then does the keyset work:

```csharp
		// Cursor over the fixed (CreatedAt DESC, NotificationId DESC) ordering. An
		// undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(request.Cursor, 2);
```

```csharp
		var hasSeek = afterCreatedAt.HasValue && afterNotificationId.HasValue;
		var pageSize = KeysetPage.Clamp(request.PageSize);

		var rows = await _notificationRepository.GetNotificationsPageAsync(
			recipientUserId,
			hasSeek ? afterCreatedAt : null,
			hasSeek ? afterNotificationId : null,
			pageSize + 1,
			unreadOnly,
			cancellationToken);

		var (page, hasMore) = KeysetPage.Trim(rows, pageSize);
```

`hasSeek` requires **both** fields — a cursor that decoded but only parsed one is treated as no
cursor at all, so a half-valid cursor restarts the walk instead of skipping rows. `pageSize + 1`
plus `KeysetPage.Trim` is the standard "is there another page without a second query" trick
(`BuildingBlocks/Pagination/KeysetPage.cs`):

```csharp
	// items must have been fetched with Take(pageSize + 1); the extra row only signals
	// that a next page exists and is trimmed here.
	public static (List<T> Items, bool HasMore) Trim<T>(List<T> items, int pageSize)
	{
		var hasMore = items.Count > pageSize;
```

The total count is computed only when there is no seek:

```csharp
		long? totalCount = hasSeek
			? null
			: await _notificationRepository.CountNotificationsAsync(
				recipientUserId,
				unreadOnly,
				cancellationToken);
```

matching `KeysetPaginatedResult`'s own contract — *"Populated only on the first page
(request.Cursor == null); cursor pages return null and callers reuse the count captured on page
one."* The UI mirror in `DTO/SharedDTO/KeysetPaginatedResult.cs` repeats the comment verbatim, which
is the only thing keeping the two in step.

### 5.2 The other three slices — diff only

| | Route / verb | Request | Response | Difference from §5.1 |
|---|---|---|---|---|
| Unread count | `GET getunreadnotificationcount` | none | `GetUnreadNotificationCountEndpointResponse(NotificationUnreadCountDTO Count)` | No validator, no pagination. Handler returns `new NotificationUnreadCountDTO()` (i.e. `0`) when the token has no user id |
| Mark one read | `PATCH marknotificationread` | `MarkNotificationReadEndpointRequest(Guid NotificationId)` | `.Produces<bool>` — returns `Results.Ok(response.Success)`, a **bare bool** | Has a validator (`.NotEmpty()`). Returns `false` rather than throwing when there is no user id |
| Mark all read | `PATCH markallnotificationsread` | none | `.Produces<int>` — returns `Results.Ok(response.UpdatedCount)`, a **bare int** | No validator, no request body. Returns `0` when there is no user id |

The two commands build a response record and then return a scalar out of it:

```csharp
			var response = new MarkNotificationReadEndpointResponse(result.Success);

			return Results.Ok(response.Success);
```

`response` is otherwise unused. That is deliberate — the UI deserialises a bare `bool`/`int`
(`ApiRequestExtensions.SendAsync<bool>`), so the record exists only to name the contract in the
`.Produces<T>` metadata. **Changing it to `Results.Ok(response)` breaks the client silently**, and
it is the one place in the feature where the two commands and the two queries disagree on envelope
shape.

The `false`-for-everything rule is documented on the endpoint rather than left to inference:

```csharp
		.WithDescription(
			"Marks one of the caller's own notifications as read. Returns false when the "
			+ "notification is unknown, already read, or belongs to another user - the "
			+ "three are deliberately indistinguishable so the response cannot be used to "
			+ "probe for somebody else's notification ids.")
```

### 5.3 Where the isolation actually lives

Not in the handlers — they merely decline to widen it. The repository interface states the rule and
every method honours it:

```csharp
/// <remarks>
/// Every read takes the recipient id as its first argument and every implementation
/// filters on it. That is the isolation boundary: a caller can only ever reach their own
/// notifications, and it is enforced here rather than being left to each handler to
/// remember.
/// </remarks>
```

On the write side the recipient is part of the `UPDATE` predicate rather than a lookup-then-check:

```csharp
	// The recipient predicate is part of the UPDATE rather than a lookup-then-check, so
	// another user's id simply matches no rows instead of being fetched and rejected.
	public async Task<bool> MarkAsReadAsync(
		Guid recipientUserId,
		Guid notificationId,
		CancellationToken cancellationToken)
	{
		var updated = await _dbContext.Notifications
			.Where(notification => notification.NotificationId == notificationId
				&& notification.RecipientUserId == recipientUserId
				&& !notification.IsRead)
			.ExecuteUpdateAsync(
				setters => setters
					.SetProperty(notification => notification.IsRead, true)
					.SetProperty(notification => notification.ReadAt, DateTime.UtcNow),
				cancellationToken);

		return updated > 0;
	}
```

`&& !notification.IsRead` is what makes an already-read row return `false`, and it is also what
makes `MarkAsReadAsync` idempotent at the database level — a second click updates zero rows rather
than moving `ReadAt`. `MarkAllAsReadAsync` is the same shape without the id, returning the row count.

Both use `ExecuteUpdateAsync`, so no entity is ever materialised and no `SaveChangesAsync` is
involved. That matters for §9.7: the *reads* cannot accidentally commit anything.

The ordering and the seek must agree, and the repository says so:

```csharp
	// Newest first, unique NotificationId as the tiebreaker. ApplySeek must mirror this
	// expression exactly. Matches IX (RecipientUserId, CreatedAt DESC, NotificationId DESC).
	private static IQueryable<AtsNotification> ApplyOrder(IQueryable<AtsNotification> query) =>
		query
			.OrderByDescending(notification => notification.CreatedAt)
			.ThenByDescending(notification => notification.NotificationId);

	private static IQueryable<AtsNotification> ApplySeek(
		IQueryable<AtsNotification> query,
		DateTime afterCreatedAt,
		Guid afterNotificationId) =>
		query.Where(notification => notification.CreatedAt < afterCreatedAt
			|| (notification.CreatedAt == afterCreatedAt
				&& notification.NotificationId.CompareTo(afterNotificationId) < 0));
```

`Guid.CompareTo` inside an EF predicate is the established keyset idiom in this repository — the
same expression appears in `AtsAuditRepository:158` (also `< 0`, also newest-first),
`ATSRepository.Reports:193`, `OMSTicketingRepository:557` and five others. The projection is a
`static readonly Expression<...>` field rather than an inline lambda so it is built once.

`BuildRowsQuery` is the single place `unreadOnly` is applied, shared by the page and the count so
the two cannot disagree about what "unread" means:

```csharp
	private IQueryable<AtsNotification> BuildRowsQuery(Guid recipientUserId, bool unreadOnly)
	{
		var query = _dbContext.Notifications
			.AsNoTracking()
			.Where(notification => notification.RecipientUserId == recipientUserId);

		if (unreadOnly)
		{
			query = query.Where(notification => !notification.IsRead);
		}

		return query;
	}
```

`GetUnreadCountAsync` is the one read that does **not** go through it — it is a standalone
`AsNoTracking().Where(...).LongCountAsync`, so it never pays for the `unreadOnly` branch.

---

## 6. Retention — `BackgroundJobs/Notifications/AtsNotificationRetentionService.cs`

A `BackgroundService` on a `PeriodicTimer`, modelled on `AtsAuditRetentionService`. The whole
scheduling loop:

```csharp
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.RetentionEnabled)
		{
			return;
		}

		var retentionInterval = TimeSpan.FromHours(
			Math.Max(1, _options.RetentionIntervalHours));

		using var timer = new PeriodicTimer(retentionInterval);

		do
		{
			try
			{
				await SweepAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				_logger.LogError(exception, "ATS notification retention failed");
			}
		}
		while (await timer.WaitForNextTickAsync(stoppingToken));
	}
```

`do/while` rather than `while`: **the first sweep runs immediately at startup**, before the first
tick. `Math.Max(1, ...)` on the interval and `Math.Max(100, ...)` on the batch size mean a
zero-or-negative override degrades to a sane value instead of throwing out of `PeriodicTimer`'s
constructor or spinning. The generic `catch` is correct here and is not the thing
`docs/feature-development-guide.md` warns about — this is a hosted service with no request pipeline
above it, so a swallowed-and-logged exception is the only way to keep the timer alive.

The sweep:

```csharp
	private async Task SweepAsync(CancellationToken cancellationToken)
	{
		var batchSize = Math.Max(100, _options.RetentionBatchSize);
		var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _options.RetentionDays));

		int deleted;

		do
		{
			using var scope = _scopeFactory.CreateScope();

			var dbContext = scope.ServiceProvider.GetRequiredService<ATSDBContext>();

			var expiredIds = dbContext.Notifications
				.Where(notification => notification.CreatedAt < cutoff)
				.OrderBy(notification => notification.CreatedAt)
				.Select(notification => notification.NotificationId)
				.Take(batchSize);

			deleted = await dbContext.Notifications
				.Where(notification => expiredIds.Contains(notification.NotificationId))
				.ExecuteDeleteAsync(cancellationToken);
```

`using var scope` is **inside** the loop, so each batch gets a fresh `ATSDBContext` — a long sweep
does not accumulate change-tracker state. `expiredIds` is an `IQueryable`, not a list, so the whole
thing is one `DELETE ... WHERE "NotificationId" IN (SELECT ... ORDER BY ... LIMIT n)` round trip
per batch rather than a select followed by a delete. `OrderBy(CreatedAt)` ascending is what the
third index in §1.2 exists for.

Termination is `while (deleted >= batchSize && !cancellationToken.IsCancellationRequested)` — a
short batch means the backlog is cleared. Configuration, `Configuration/AtsNotificationOptions.cs`:

```csharp
public sealed class AtsNotificationOptions
{
	public const string SectionName = "AtsNotifications";

	public bool RetentionEnabled { get; set; } = true;

	// One month. A notification older than this has either been acted on or overtaken by
	// the state it was pointing at, and the row is only useful for the badge and the
	// dropdown - neither of which looks back that far.
	public int RetentionDays { get; set; } = 30;

	public int RetentionIntervalHours { get; set; } = 24;

	public int RetentionBatchSize { get; set; } = 5_000;
}
```

Every value has a working default, so the feature ships with no appsettings entry — confirmed: no
`AtsNotifications` section appears in any `appsettings.*.json`. The sweep is by `CreatedAt` alone,
so **an unread notification is deleted at 30 days** and the badge drops by itself (§9.12).

---

## 7. Caching — what is deliberately not cached, and the one thing that is

`AtsNotificationRepository` opens with the decision:

```csharp
// Deliberately NOT cached, and no ATSCacheRepository decorator: the whole point of the
// bell is to show what just happened, so a cached unread count or first page would hide
// the notification that was raised a second ago. Same reasoning as AtsAuditRepository and
// OMSTicketingRepository.
public sealed class AtsNotificationRepository : IAtsNotificationRepository
```

and the registration matches — a direct `AddScoped` with no `Decorate`, unlike most ATS
repositories which forward through the decorated aggregate:

```csharp
		// Uncached for the same reason as the two above: the bell exists to show what just
		// happened, so a cached unread count would hide the notification raised a second ago.
		services.AddScoped<IAtsNotificationRepository, AtsNotificationRepository>();
```

**So there is no invalidation problem, because there is no cache to invalidate.** The badge is a
live `LongCountAsync` on every call, and the "cached unread count that does not invalidate" hazard
does not exist on the read side. What keeps the badge current between page loads is not a cache at
all — it is `UnreadCount++` in the SignalR handler plus a re-read on `Reconnected` (§8.1).

Do not "normalise" this registration to match its siblings. Adding `services.Decorate<IAtsNotificationRepository, ...>`
would introduce exactly the staleness the design forbids, and there is no tag to hang invalidation
on: `RaiseAsync` writes through the repository, so a decorator would have to invalidate on
`AddAsync`, on both mark-read methods, *and* on the retention sweep — which bypasses the repository
entirely and talks to `ATSDBContext` directly (§6). That last one is unreachable from a decorator,
so a cached count would silently drift every 24 hours.

**The one cached read in the feature** is the administrator roster used by the §4.5 fan-out, in
`Data/Cache/Users/ATSCacheRepository.Users.Cache.cs`:

```csharp
	// Cached: the admin roster changes when somebody is added or edited, and both of those
	// already invalidate CacheTags.User. Read once per email pass rather than per message, but
	// there is no reason to ask the database for a list that only changes on a user write.
	public async Task<IReadOnlyList<Guid>> GetAtsAdministratorUserIdsAsync(CancellationToken cancellationToken) =>
		await _hybridCache.GetOrCreateAsync<List<Guid>>(
			"ats_administrator_user_ids",
			async token => (await _atsRepository.GetAtsAdministratorUserIdsAsync(token)).ToList(),
			tags: [CacheTags.User, CacheTags.Role], cancellationToken: cancellationToken);
```

The comment's claim checks out: `RemoveByTagAsync(CacheTags.User)` fires from the add-user path
(line 36), `EditUserAsync` (line 72), the user-client decorator (line 48) and the role decorator
(line 37). A newly promoted admin therefore reaches the fan-out on the next pass without a restart.
This is a **global** key with no user scope, which is correct here precisely because the value is
not user-specific — but it is the pattern `docs/feature-development-guide.md` warns about, so do not
copy it for anything that is.

---

## 8. The browser side

All under `UI/FrontendWebassembly/`.

### 8.1 `Services/ATS/Notifications/NotificationService.cs` — connection lifecycle

Registered `services.AddScoped<INotificationService, NotificationService>();`
(`ServiceConfig/FrontendServiceConfig.cs:80`). In Blazor WASM the root scope lives for the whole
app, so **scoped is effectively singleton**: one instance, one socket, one badge, for the session.

The contract, `INotificationService.cs`:

```csharp
public interface INotificationService : IAsyncDisposable
{
	/// <summary>Unread count for the badge. Seeded from the API, then kept current by the hub.</summary>
	long UnreadCount { get; }

	/// <summary>Raised when a notification arrives or the unread count changes.</summary>
	event Action<NotificationDTO?>? NotificationsChanged;
```

The nullable argument **is** the protocol: non-null means "here is the row that just arrived", null
means "the count moved, refetch if you are displaying rows". Both components branch on it.

`StartAsync` seeds before it connects, and the order is the point:

```csharp
	public async Task StartAsync()
	{
		// Seed the badge first so it is right even if the hub never connects - the rows
		// are already in the database either way.
		await RefreshUnreadCountAsync();

		if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
		{
			return;
		}

		var baseUri = _httpClient.BaseAddress?.ToString()?.TrimEnd('/') ?? string.Empty;
		var hubUrl = $"{baseUri}/hubs/atsbulk";
```

Note where the guard sits — *after* the await. See §9.3.

### 8.2 How a websocket authenticates when the token is an HttpOnly cookie

The browser cannot read the cookie, so the client cannot put it in an `Authorization` header. The
answer is to make the handshake a credentialed request and let the browser attach the cookie itself:

```csharp
		_hubConnection = new HubConnectionBuilder()
			.WithUrl(hubUrl, options =>
			{
				// Without this the auth cookie is not sent on the handshake, ATSHub's
				// Context.User is null, GetUserGroupName returns null, and the connection
				// joins no group - so nothing is ever delivered. It works in deployed
				// environments only because the gateway serves the UI and the API from one
				// origin; in local development they are different ports and the cookie is
				// dropped. CookieHandler sets Include + Cors, which is what the handshake
				// needs.
				options.HttpMessageHandlerFactory = innerHandler =>
					new CookieHandler { InnerHandler = innerHandler };
			})
			.WithAutomaticReconnect()
			.Build();
```

`HttpMessageHandlerFactory` wraps only the transport handler, not the named `"API"` client, so the
`InterceptorHandler` refresh logic does not apply to the socket. The full chain is: cookie rides the
`negotiate` POST → ASP.NET Core authentication middleware builds a `ClaimsPrincipal` → SignalR
copies it onto `HubCallerContext.User` → `GetUserGroupName()` reads `ClaimTypes.NameIdentifier` →
`Groups.AddToGroupAsync`. **A failure at any hop is silent** — the connection succeeds, joins
nothing, and simply never receives. That is why the local-dev symptom was "the bell works in
production and is dead on `:5134`".

The same `CookieHandler` block appears verbatim in `Services/ATS/EndorsementSubmission/EndorsementSubmissionService.cs`,
with a comment recording that it repaired the bulk-upload toast. It does **not** appear in
`Services/ATS/AIAssistant/AtsAssistantService.cs` (§9.5).

### 8.3 Reconnection and disposal

```csharp
		// A reconnect means the badge may have moved while the socket was down, so it is
		// re-read rather than assumed.
		_hubConnection.Reconnected += async _ => await RefreshUnreadCountAsync();

		_hubConnection.Closed += async ex =>
		{
			_logger.LogWarning(ex, "Notification hub connection closed.");
			await Task.CompletedTask;
		};

		// The bell still works without a live socket: the count is seeded above and every
		// page load re-reads it, so a failed connection must not break the layout.
		await StartHubSafelyAsync();
```

`WithAutomaticReconnect()` with no arguments is SignalR's default schedule — retry at 0s, 2s, 10s
and 30s, then give up and fire `Closed`. The `Reconnected` handler is what makes a dropped socket
recoverable *for the badge*: the reconnect handshake is credentialed, so the socket rejoins the
right group, and the count is re-read rather than incremented from a possibly-stale value. Note
that rows raised while the socket was down are **not** back-filled into an open dropdown — only the
count is corrected. `NotificationsPage` re-reads on filter change; the bell re-reads every time it
opens.

The one place the service deliberately breaks the no-try/catch rule, with the reason:

```csharp
	// SignalR's StartAsync throws on an unreachable hub rather than returning a status, and
	// there is no ServiceResponse-shaped equivalent to route it through. Kept to this one
	// method so the rest of the service stays free of transport handling.
	private async Task StartHubSafelyAsync()
	{
		try
		{
			await _hubConnection!.StartAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not open the notification hub connection.");
		}
	}
```

Disposal:

```csharp
	public async ValueTask DisposeAsync()
	{
		if (_hubConnection is not null)
		{
			await _hubConnection.DisposeAsync();
			_hubConnection = null;
		}

		GC.SuppressFinalize(this);
	}
```

Correct in isolation — and, because the service is scoped to the app's root, **never called in
practice**. Nothing stops the connection on logout (§9.2).

### 8.4 Reads and local count adjustment

```csharp
	public Task<ServiceResponse<KeysetPaginatedResult<NotificationDTO>>> GetNotificationsAsync(
		string? cursor = null,
		int pageSize = 15,
		bool unreadOnly = false)
	{
		var query = $"ats/getnotifications?pageSize={pageSize}&unreadOnly={unreadOnly}";

		if (!string.IsNullOrEmpty(cursor))
			query += $"&cursor={Uri.EscapeDataString(cursor)}";

		return ApiRequestExtensions.SendAsync<GetNotificationsResponseDTO, KeysetPaginatedResult<NotificationDTO>>(
			() => _httpClient.GetAsync(query),
			result => result.Notifications);
	}
```

URLs are relative to the gateway with **no leading slash**, matching `MatchPath` in §10. `pageSize`
and `unreadOnly` are not escaped because both are non-string; `cursor` is, because it is base64 and
can contain `+` and `/`. `unreadOnly` interpolates as `True`/`False`, which ASP.NET Core's `bool`
binder accepts case-insensitively.

The two-argument `SendAsync<TResponse, TResult>` overload is what unwraps the endpoint's envelope —
`select` runs after the status check, so a null `Notifications` becomes *"The server returned an
empty response."* rather than a `NullReferenceException`.

The writes adjust the badge locally:

```csharp
		if (response.IsSuccess && response.Data && UnreadCount > 0)
		{
			// Adjusted locally rather than re-read: the caller is usually navigating away,
			// and a round trip just to decrement by one is wasted.
			UnreadCount--;
			NotificationsChanged?.Invoke(null);
		}
```

`response.Data` is the bare `bool` from §5.2, so a server-side `false` (not yours, or already read)
correctly leaves the badge alone. `MarkAllAsReadAsync` sets `UnreadCount = 0` unconditionally on
success rather than subtracting the returned `UpdatedCount` — the count is authoritative either way,
since only unread rows can be in it.

`RefreshUnreadCountAsync` is the one method that writes the field from the server, and it signals
with `null`:

```csharp
		UnreadCount = response.Data!.UnreadCount;
		NotificationsChanged?.Invoke(null);
```

### 8.5 The two components — and the unsubscribe rule

`Component/ATS/Notifications/NotificationCenter.razor.cs` (216 lines) is mounted **once**, in
`Layout/ATSLayout.razor:158`:

```razor
                    @* Mounted here rather than per page so the hub connection is opened
                       once and stays live across ATS navigation. *@
                    <NotificationCenter />
```

Both components implement `IDisposable` and both unsubscribe. This is the fix for
`docs/reviews/ats-oneplatform-fix-details.md` item 9, and it **is** present here:

```csharp
	public void Dispose()
	{
		// Mandatory, not tidiness: the service is scoped, which in WASM means it lives as
		// long as the app. Without this every navigation would leave another subscription
		// behind and the user would get one duplicate toast per visit.
		Notifications.NotificationsChanged -= OnNotificationsChanged;
	}
```

`NotificationsPage.Dispose` does the same, plus observer teardown (§8.6). The `@implements IDisposable`
directive is in both `.razor` files.

The hub event is a plain `Action`, so the boundary has to be `void`, and the async body is separated
so nothing throws into the SignalR callback:

```csharp
	// The hub event is a plain Action, so the boundary has to be void. The body lives in
	// an async Task so an exception cannot be thrown into the SignalR callback where
	// nothing is able to catch it - same reasoning as NewOrderComponent.OnATSResponse.
	private void OnNotificationsChanged(NotificationDTO? arrived) =>
		_ = OnNotificationsChangedAsync(arrived);
```

`InvokeAsync` is what marshals onto the renderer's synchronisation context, and only
`ObjectDisposedException` is caught:

```csharp
		catch (ObjectDisposedException)
		{
			// The component went away between the hub event and the render. Nothing to do.
		}
```

The bell's toast filter, with the reasoning that a batch of 40 produced 40 toasts:

```csharp
	private static bool ShouldToast(string type) => type switch
	{
		AtsNotificationTypes.TicketingFailed => false,
		AtsNotificationTypes.InvitationEmailFailed => false,
		_ => true
	};
```

`EmailAccountsExhausted` is **not** in that list, so the §4.5 fan-out toasts (§9.1).

`PreviewCount = 8` in the bell, `PageSize = 20` on the page — two independent constants that both
have to stay inside the validator's 1..100. The badge caps at `99+` for a layout reason, not a
semantic one:

```csharp
	// Caps at 99+: a three-digit count would push the pip out past the button's corner,
	// and the exact number stops being actionable long before then.
	private string UnreadLabel =>
		Notifications.UnreadCount > 99 ? "99+" : Notifications.UnreadCount.ToString();
```

`NotificationItem.razor` is shared by both screens so they cannot drift, and is a `<button>` rather
than an `<a>` for a stated reason:

```razor
@* One row, shared by the bell dropdown and the full notifications page so the two can
   never drift apart. Rendered as a button rather than an anchor even when it has a link:
   activating it marks the notification read first and then navigates, which an <a> would
   race. *@
```

`RelativeTime` in `NotificationItem.razor.cs` handles clock skew explicitly rather than letting a
negative elapsed render:

```csharp
			if (elapsed < TimeSpan.Zero)
			{
				// Clock skew between the server and the browser. Reads better than
				// "-2s ago".
				return "just now";
			}
```

### 8.6 Infinite scroll

`NotificationsPage.razor.cs` owns a `DotNetObjectReference` and a JS observer handle. The observer
is attached in `OnAfterRenderAsync` rather than on first render, because the sentinel does not exist
until a cursor does:

```csharp
	// The sentinel only exists once a page has rendered and a next cursor is present, so
	// the observer is attached after each render rather than once on first render.
	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (_nextCursor is null || _observer is not null || _items.Count == 0)
		{
			return;
		}
```

`wwwroot/js/ats/notificationScroll.js` returns a handle rather than registering a global, so a page
that reloads its list can tear down its own observer:

```javascript
    // Returns a handle the caller disposes. One observer per handle, so a page that
    // reloads its list can tear down the old one without touching any other.
    observe: function (sentinel, dotNetRef) {
```

```javascript
                {
                    // Start loading a little before the sentinel is actually on screen, so
                    // the next page is usually there by the time the reader reaches it.
                    rootMargin: '200px 0px'
                });
```

The JS callback is fire-and-forget because the .NET side guards against overlap itself:

```csharp
	private async Task LoadMoreAsync()
	{
		// Guarded because the observer can fire again while a page is still in flight.
		if (_isLoadingMore || _nextCursor is null)
		{
			return;
		}
```

Teardown uses `SafeJs`, the client-side counterpart to `SideEffectGuard`, which swallows only the
two exceptions that mean "already gone":

```csharp
		var observer = _observer;

		// Cleared first: disposal runs during navigation and can yield, and a second
		// caller must not find a handle that is already being torn down.
		_observer = null;

		// The handle owns the IntersectionObserver; dispose() disconnects it.
		await SafeJs.InvokeVoidAsync(observer, "dispose");

		await SafeJs.DisposeAsync(observer);
```

The `Load more` button inside the sentinel is the keyboard and no-JS path — the markup comment says
so, and it is a real `<button>` with `@onclick="LoadMoreAsync"`.

### 8.7 Route access — why `/s&i/ats/notifications` is not an ATS module

The page carries `@attribute [RequirePermission(6, 7)]` and `@inherits SecurePageBase` — that is
the *platform* application/submenu pair (`SubMenuList.cs:14` is `{ 7, ("ats", "ATS", ...) }`), i.e.
"can reach ATS at all". It has **no** `[RequireATSModule]`, because notifications is deliberately
absent from `ShareData/ATS/ModuleList.cs`:

```csharp
			// Notifications (/s&i/ats/notifications) is deliberately NOT here. This list
			// drives both the sidebar and ATSLayout.CanAccessRoute, and every id in it must
			// exist in the backend module seed data and be grantable. Notifications is not a
			// permissioned module - anyone with ATS access has an inbox - so adding it would
			// show a link only super admins could follow and bounce everyone else to
			// /access-denied. The page is reached from the bell instead.
```

Which forces an explicit carve-out in `ATSLayout.CanAccessRoute` (`Layout/ATSLayout.razor:290`),
since that method redirects anything under `s&i/ats/` whose segment it cannot map:

```csharp
		// Notifications is not a permissioned module - everyone who can reach ATS at all
		// has an inbox, and it only ever shows that user's own rows. It is therefore
		// deliberately absent from ModuleList, which would otherwise put an ungrantable
		// entry in the sidebar; the check has to allow it explicitly instead.
		if (string.Equals(routeSegment, "notifications", StringComparison.OrdinalIgnoreCase))
			return true;
```

**Add module 17 for notifications and the page breaks** — `ModuleList` would match the segment
first, `module.Key > 0` would be true, and every user without the grant would be redirected. The
absence from `ModuleList` is load-bearing, in two files.

The `CanOpen` courtesy check in both components depends on the same list, and fails open by design:

```csharp
	// Maps a link back to the ATS module that owns it, using ModuleList as the single
	// source of truth for route segments rather than a second hardcoded list that would
	// drift from it. Anything unrecognised is allowed: the destination page runs its own
	// RequireATSModule check, so this is a courtesy that avoids a pointless bounce, not
	// the access control itself.
	private bool CanOpen(string linkUrl)
	{
		if (AccessibleModuleIds is null || AccessibleModuleIds.Count == 0)
		{
			return true;
		}

		// Strip the query string before matching: "?search=Juan" must not be mistaken for
		// part of the route segment.
		var path = linkUrl.Split('?', '#')[0].TrimEnd('/');

		var module = ModuleList.List.FirstOrDefault(entry =>
			path.EndsWith($"/{entry.Value.path}", StringComparison.OrdinalIgnoreCase));

		return module.Key == 0 || AccessibleModuleIds.Contains(module.Key);
	}
```

`module.Key == 0` is the "unrecognised" branch of a `FirstOrDefault` over a
`Dictionary<int, ...>` — it means *allow*. The two copies differ only in where the module set comes
from: the bell takes it from a cascading parameter supplied by `ATSLayout`, the page from
`SecurePageBase.AccessibleATSModuleIds`. Both are empty-set-means-allow, which is safe only because
the destination page enforces the real check.

### 8.8 Styling

Row styles live in `wwwroot/css/ats.css` (lines 6399-6580), not in either component's scoped sheet:

```css
/* ---------- Notification rows ----------

   Shared by the bell dropdown (NotificationCenter) and the full notifications page, which
   is why they live here rather than in either component's scoped sheet: the two must not
   drift, and NotificationItem is rendered inside both scopes.

   Colour comes entirely from the --c-* tokens, so both themes are covered by one set of
   rules. See docs/ui-theming-and-responsiveness.md. */
```

`.ats-notif-icon.is-info` / `.is-success` / `.is-warn` / `.is-danger` / `.is-neutral` are the five
classes `NotificationItem.AccentModifier` emits — **that switch and these five selectors are the
whole contract**, and adding a sixth accent needs both. Scoped sheets hold only what is unique to
each screen: `NotificationCenter.razor.css` (131 lines) the overlay and the 380px panel,
`NotificationsPage.razor.css` (122 lines) the header and sentinel. The panel's phone override:

```css
/* Phone: a 380px dropdown pinned to the bell would hang off the screen, so it becomes a
   near-full-width sheet anchored to the viewport instead of the button. */
@media (max-width: 600px) {
    .ats-notif-panel {
        position: fixed;
        top: 64px;
        right: 12px;
        left: 12px;
        width: auto;
        max-width: none;
        max-height: calc(100dvh - 88px);
    }
}
```

The bell button itself reuses `.ats-console-icon-btn` from the topbar and the badge reuses
`.ats-console-notification-dot` from `Layout/ATSLayout.razor.css` — the scoped sheet's header
comment says so explicitly, which is why it is only 131 lines.

---

## 9. Sharp edges

Ordered by how badly they bite. None of these is fixed here.

### 9.1 `EmailAccountsExhausted` has no cooldown — one notification per admin every five seconds

`RaiseAccountsExhaustedAsync` is called from the pass's **early-out**, which runs on every tick
while no account can send:

```csharp
		if (!await HasSendableAccountAsync(cancellationToken))
		{
			_logger.LogWarning(
				"Skipping email pass: every registered sender account is capped, cooling down, unverified or disabled.");

			await RaiseAccountsExhaustedAsync(cancellationToken);

			return;
		}
```

and the trigger is `WithIntervalInSeconds(5).RepeatForever()`
(`BackgroundJobs/EmailNotification/EmailNotificationBackgroundJobSetup.cs`). There is no
"already notified" flag, no cooldown, and no dedupe on `(recipient, type)`. A daily sender cap that
does not lift for 24 hours therefore produces **~17,280 identical rows per administrator**, at 12 a
minute — and `ShouldToast` returns `true` for this type, so each one also raises a MudBlazor
snackbar. The rows are individually correct; the volume is the defect. The bell's `PreviewCount = 8`
means the dropdown shows nothing but this message, and the badge saturates at `99+` within eight
minutes.

The unit test pins the wrong side of it: `Times.AtLeastOnce`, not `Times.Once`. Contrast §4.3,
where `GetCompletedBulkEmailFilesAsync` asks the database whether the condition is newly true — the
same shape would fix this, e.g. only raise when a previous pass had a sendable account.

### 9.2 The hub connection survives logout, and the next user inherits it

`INotificationService` is `AddScoped`, which in Blazor WASM means one instance for the whole
session, and `DisposeAsync` therefore never runs. The ATS logout path is a **soft** navigation —
`Layout/MainLayout.razor.cs:163-178`:

```csharp
			var logout = await IAuthService.Logout();


			if (logout)
			{
				Console.WriteLine(logout ? "Logout successful." : "Logout failed.");
				Navigation.NavigateTo("/login");
```

No `forceLoad`, no `location.reload` (the only forced reload in the app is `SSOLayout.razor.cs:58`,
and `ATSLayout.razor:214` which fires only on an unauthenticated first load). `AuthService.Logout`
clears `localStorage` and the server clears the cookie — but the **websocket is already open**, and
SignalR group membership was fixed at connect time. Nothing revokes it. The socket stays in user
A's group, pinging happily, while the app renders the login page.

Now user B logs in **in the same tab** and opens ATS. `NotificationCenter.OnInitializedAsync` calls
`StartAsync()`, which seeds B's count correctly and then short-circuits:

```csharp
		if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
		{
			return;
		}
```

B's bell is now attached to A's socket. Every notification raised for A increments B's badge,
inserts A's row at the top of B's dropdown, and toasts A's title into B's screen. Clicking it calls
`marknotificationread` **as B**, which matches zero rows (§5.3), so the row stays unread for A and
B's local `IsRead = true` is never confirmed.

The server-side guard from §3.2 is sound — it correctly refuses to let anyone *choose* a group. What
it cannot defend against is a client that never hangs up. The precondition is a shared browser
(kiosk, shared workstation, an untouched family machine); I traced the chain through the code but
did not run it. The fix is a `StopAsync`/`DisposeAsync` on logout, or a `forceLoad` navigation.

### 9.3 `StartAsync` is not idempotent, and leaks a live connection when it is called twice

Two problems in the same method. First, the guard runs *after* an await, so two callers interleave:

```csharp
		await RefreshUnreadCountAsync();          // <-- yields here

		if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
		{
			return;
		}
```

`NotificationCenter.OnInitializedAsync` and `NotificationsPage.OnInitializedAsync` both call it, and
on a deep link straight to `/s&i/ats/notifications` both components initialise in the same render
pass. The layout's call suspends at the HTTP await; the page's call passes the null check; both
build a `HubConnection` and both assign to `_hubConnection`. There is no lock, no
`SemaphoreSlim(1,1)`, and no `Interlocked` guard.

Second, the guard only short-circuits on `Connected`. A connection that exists but is
`Disconnected` — after `WithAutomaticReconnect` exhausts its four attempts, or after
`StartHubSafelyAsync` swallowed a failure — falls through and is **overwritten without being
disposed**.

Either way the orphan is still connected to the server, still in the user's group, and still has
its handler registered:

```csharp
		_hubConnection.On<NotificationDTO>("ReceiveNotification", notification =>
		{
			UnreadCount++;
			NotificationsChanged?.Invoke(notification);
		});
```

`Clients.Group(...)` delivers to every connection in the group, so one notification now runs that
lambda twice on the same service instance: the badge increments by two and two toasts appear. This
is the item-9 symptom (duplicate notifications) arriving by a different route — the components
unsubscribe correctly; the *service* has two sockets.

### 9.4 Three connections per browser tab to one hub

`NotificationService`, `EndorsementSubmissionService` and `AtsAssistantService` each build their own
`HubConnection` to the same `/hubs/atsbulk`. One user in one tab therefore occupies **three**
connection ids in the same SignalR group, and every `Clients.Group(...)` push is serialised and
delivered three times — the notification payload included, twice of them to connections that have no
`ReceiveNotification` handler and silently drop it. Not a correctness bug (the handlers are
per-connection), but it triples the socket count and the per-push work, and it means a
`GetExhaustedTicketIdsAsync`-style burst is amplified before it reaches the browser.

### 9.5 `AtsAssistantService` still sends `?userId=`, and has no `CookieHandler`

`Services/ATS/AIAssistant/AtsAssistantService.cs:41`:

```csharp
		var baseUri = _httpClient.BaseAddress?.ToString()?.TrimEnd('/') ?? string.Empty;
		var hubUrl = $"{baseUri}/hubs/atsbulk?userId={Uri.EscapeDataString(userId)}";

		_hubConnection = new HubConnectionBuilder()
			.WithUrl(hubUrl)
			.WithAutomaticReconnect()
			.Build();
```

Two separate problems. The parameter is **inert** — §3.2 reads only `Context.User` — so this is dead
code that reads exactly like the vulnerability the review fixed, and is the most likely place for
someone to conclude the query string still works. And there is no `HttpMessageHandlerFactory`, so in
local development (UI `:5134`, API `:5123`) the assistant handshake carries no cookie,
`GetUserGroupName` returns null, the connection joins no group, and streamed replies never arrive.
`EndorsementSubmissionService` was fixed and says so in a comment; this one was not. Note it also
`return`s early when `localStorage` has no `UserId`, so the assistant socket is not even attempted
before login.

### 9.6 The hub is anonymous and its gateway route has no rate limit

`ATSHub` carries no `[Authorize]` (§3.2, deliberately), `MapHub<ATSHub>(...)` has no
`.RequireAuthorization()`, and the gateway entry has no `Metadata` — unlike, say, the
`WithdrawnApplicationForm` route immediately above it, which carries
`GatewayConstants.RateLimitPolicies.AnonymousApplicationForm`. Any unauthenticated client can open
and hold connections at the gateway's 500/s default. They receive nothing, because the group rule
holds, so this is resource exhaustion rather than disclosure — but there is no bound on how many
sockets one client can park, and each one costs a server-side connection plus a group-membership
entry.

### 9.7 `AddAsync` commits whatever else the ambient `DbContext` is holding

```csharp
	public async Task AddAsync(AtsNotification notification, CancellationToken cancellationToken)
	{
		_dbContext.Notifications.Add(notification);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}
```

That is a full `SaveChangesAsync` on the **scoped** `ATSDBContext`, not a notification-scoped write —
it flushes every tracked entity in the context. Every current caller raises after its own
`CommitAsync` (or, in the bulk and email jobs, after its repository call has already saved), so
nothing is pending today and this is latent rather than live. But it is the mechanism by which the
design doc's own §10 rule ("persist before push") can be inverted from the *caller* side: raise a
notification inside an open `TransactionRunner` scope and the row commits with the caller's work —
or rolls back with it, **after** the push has already gone out, producing exactly the
"toast for something absent from the inbox" the rule exists to prevent. Nothing in the type system
or in `SideEffectGuard` distinguishes the two situations.

### 9.8 The dropdown's item list races with an arrival

Both components mutate `_items` from the hub callback and from an HTTP continuation with no
coordination. `NotificationCenter.LoadPreviewAsync`:

```csharp
		var response = await Notifications.GetNotificationsAsync(pageSize: PreviewCount);

		_isLoading = false;
		...
		_items.Clear();
		_items.AddRange(response.Data!.Items);
```

A notification arriving during that await is `Insert(0, ...)`-ed into the list and then erased by
`Clear()`. The badge stays right (`UnreadCount` lives on the service), but the row the user just saw
appear vanishes from the open dropdown. The reverse ordering duplicates instead: open the dropdown
immediately after a push and the arrival can be both the inserted row and a row in the fetched page.
`NotificationsPage.LoadFirstPageAsync` has the identical shape; `LoadMoreAsync` only appends, so it
can duplicate but not lose. Neither list is keyed by `NotificationId`, so `NotificationItem` renders
whatever is there.

### 9.9 `NotificationsPage.Dispose` disposes the .NET reference before the JS observer is gone

```csharp
	public void Dispose()
	{
		// The service outlives the page, so this is mandatory - see NotificationCenter.
		Notifications.NotificationsChanged -= OnNotificationsChanged;

		_ = DisposeObserverAsync();

		_selfReference?.Dispose();
	}
```

`DisposeObserverAsync` is fire-and-forget, so `_selfReference?.Dispose()` runs while
`SafeJs.InvokeVoidAsync(observer, "dispose")` is still in flight — the `observer.disconnect()` that
stops callbacks happens *after* the reference they would target is gone. An `IntersectionObserver`
callback already queued in that window calls `dotNetRef.invokeMethodAsync('OnSentinelVisibleAsync')`
on a disposed reference. `SafeJs` absorbs `JSDisconnectedException` and `ObjectDisposedException` on
the .NET side, but the JS-side promise rejection is unobserved. Narrow, and it presents as console
noise rather than a crash.

### 9.10 `RaiseAccountsExhaustedAsync` hand-rolls the try/catch `SideEffectGuard` exists to replace

§4.5 quoted it. `docs/feature-development-guide.md` is explicit: *"A handler, service or repository
that catches an exception to log it and rethrow, or to convert it into a `false` return, is
duplicating that"*, and `SideEffectGuard` is listed as the helper for "a best-effort side effect
after the real work has committed" — which this is. The practical cost is the scope of the block: it
wraps the **roster lookup** as well as the raises, so a transient failure in
`GetAtsAdministratorUserIdsAsync` silences the entire fan-out. That is the one failure this
notification exists to make visible, and the class doc for `RaiseAccountsExhaustedAsync` says so
(*"The one failure mode of this feature that is invisible from the outside"*) while the catch makes
its own failure invisible too. Every other notification path uses the `fallback:` overload instead.

### 9.11 `Type` is the one field with no length guard

`Truncate` is applied to `Title`, `Body` and `LinkUrl` and not to `Type`, which is written verbatim
against `HasMaxLength(60)`. The longest current constant is `EmailAccountNeedsReverification` at 31
characters, so this cannot fire today — but `RaiseAsync` takes `type` as a plain `string`, and the
only thing stopping a longer value is the convention that callers pass an `AtsNotificationType`
constant. Nothing enforces it: there is no `All` array on `AtsNotificationType` (contrast
`TicketStatus`, which has one precisely so a validator can check a caller-supplied value), and no
validator anywhere inspects the type. A 61-character value turns a successful order into a failed
insert — the exact outcome the truncation helper's comment says it exists to prevent.

### 9.12 Retention deletes unread rows, and sweeps on every boot

`SweepAsync` filters on `CreatedAt < cutoff` only. An unread notification is deleted at 30 days and
the badge — a live `LongCountAsync` — drops without the user doing anything, with no record that
they never saw it. That is a defensible product decision for a 30-day window, but it is not written
down anywhere except in the query. Separately, the `do/while` in `ExecuteAsync` means the first
sweep runs at startup rather than after the first interval, so in a multi-node deployment **every
instance sweeps concurrently on boot**. `ExecuteDeleteAsync` over the same `IN (SELECT ... LIMIT n)`
subquery is safe — the second delete simply matches fewer rows — but the nodes will fight over the
same batch and log duplicate "Deleted N" lines. `AtsAuditRetentionService` has the same shape.

---

## 10. Wiring — what is registered where

### 10.1 Backend DI

`BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs`, inside `AddATSServices` — the
three registrations at lines 89-91:

```csharp
		// Uncached for the same reason as the two above: the bell exists to show what just
		// happened, so a cached unread count would hide the notification raised a second ago.
		services.AddScoped<IAtsNotificationRepository, AtsNotificationRepository>();
		services.AddScoped<IAtsNotificationService, AtsNotificationService>();
		services.AddHostedService<AtsNotificationRetentionService>();
```

and the options binding **~130 lines later**, at 221-222 — not adjacent, despite the design doc
showing all four as one block:

```csharp
		// Same story: absent section means the agreed 30-day notification retention.
		services.Configure<AtsNotificationOptions>(
			configuration.GetSection(AtsNotificationOptions.SectionName));
```

There is no `services.Decorate<IAtsNotificationRepository, ...>` (§7). Carter modules and MediatR
handlers are assembly-discovered, so the four slices need no individual registration.

### 10.2 Frontend DI

`UI/FrontendWebassembly/ServiceConfig/FrontendServiceConfig.cs:80`:

```csharp
		services.AddScoped<INotificationService, NotificationService>();
```

One line, in a block of ~35 sibling `AddScoped` calls. `Scoped` = app lifetime in WASM (§8.1, §9.2).

### 10.3 Gateway routes — `BackendAPI/Modules/ATS/Path/ATSPaths.cs:219-262`

All four under one comment, all on `GatewayConstants.OnePlatformApi`, all with a `PathSet` that
strips the `/ats` segment:

```csharp
			// ---------- In-app notifications ----------
			new RouteDefinitionDTO(
				RouteId: "GetNotifications",
				MatchPath: "/ats/getnotifications",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getnotifications" }
				}
			),
```

| RouteId | MatchPath | Method | PathSet |
|---|---|---|---|
| `GetNotifications` | `/ats/getnotifications` | GET | `/getnotifications` |
| `GetUnreadNotificationCount` | `/ats/getunreadnotificationcount` | GET | `/getunreadnotificationcount` |
| `MarkNotificationRead` | `/ats/marknotificationread` | Patch | `/marknotificationread` |
| `MarkAllNotificationsRead` | `/ats/markallnotificationsread` | Patch | `/markallnotificationsread` |

**None carries `RateLimitPolicy` metadata**, so all four fall through to the gateway's 500/s default.
Defensible for an authenticated staff console — but `getunreadnotificationcount` is called on every
ATS page load by every user, which makes it one of the highest-volume routes in the module.

The hub route is separate, at line 650, with no `Transforms` and both GET and POST (§3.4).

Verify all five at runtime with `GET /__routes` on the gateway — the typed modules are the only
source of routes; nothing in `appsettings.*.json` is read.

### 10.4 Hub mapping

`app.MapHub<ATSHub>(configuration["SignalRHub:ATSBulkEndpoint"]!)` in
`BackendAPI/API/APIs/ServiceConfig/AppConfiguration.cs:134`, skipped entirely under the `Testing`
environment. The key resolves from `SIGNALRHUB__ATSBULKENDPOINT`; every appsettings file carries
only the `${...}` placeholder, so **the path exists nowhere as a literal on the server side** and
must be set to `/hubs/atsbulk` in the environment for the gateway route to reach it.

### 10.5 Strings that must agree across files, with nothing enforcing them

| String | Where it is written | Where it must match | Failure mode |
|---|---|---|---|
| `recipientUserId.ToString()` | `AtsNotificationService.RaiseAsync` (the push) | `GetUserGroupName()`'s `userId.ToString()` (the join) | Both are default "D" format. Change either to `"N"` or `"B"` and every push goes to an empty group — silently, with the row saved and the badge correct on reload |
| `"ReceiveNotification"` | `NotificationService`'s `On<NotificationDTO>(...)` | `IATSClient.ReceiveNotification` | The server side is strongly typed; the client is a raw string. Renaming the interface method compiles and breaks delivery |
| `/hubs/atsbulk` | `ATSPaths.cs:653` `MatchPath`, `NotificationService.cs:33`, `EndorsementSubmissionService.cs:34`, `AtsAssistantService.cs:41` | `SignalRHub:ATSBulkEndpoint` (environment only) | Four literals plus one env var, in three assemblies and one deployment config |
| `getnotifications` etc. | `MapGet("getnotifications")` | `PathSet` in `ATSPaths.cs` | and the URL literal in `NotificationService.GetNotificationsAsync` — three literals, three assemblies |
| `NotificationListDTO` properties | `BackendAPI/Modules/ATS/DTO/AtsNotificationDTO.cs` | `NotificationDTO` in `UI/.../DTO/ATS/NotificationDTO.cs` | JSON binds by name; a rename is a silent default value, not a compile error |
| `GetNotificationsEndpointResponse.Notifications` | The endpoint record | `GetNotificationsResponseDTO.Notifications` | `SendAsync<TResponse, TResult>`'s `select` returns null → *"The server returned an empty response."* |
| `AtsNotificationType.*` | `BackendAPI/Modules/ATS/Constants/` | `AtsNotificationTypes.*` in `UI/.../ShareData/ATS/` | Ten for ten today. A backend-only addition renders with the neutral bell |
| `TitleMaxLength`/`BodyMaxLength`/`LinkUrlMaxLength` | `AtsNotificationService` constants | `HasMaxLength` in `AtsNotificationConfiguration` | A widened column silently keeps truncating; a narrowed one throws on insert |
| `/s&i/ats/searchreport`, `/ticketingstatus`, `/bulkuploads`, `/emailaccounts` | `BuildOrderLink` and the two call-site links | The `path` values in `ShareData/ATS/ModuleList.cs` | `CanOpen` matches on the last segment; a mismatch falls through to `module.Key == 0` and *allows*, so the user is bounced off `/access-denied` by the destination instead |
| `ApplyOrder` vs `ApplySeek` | Both in `AtsNotificationRepository` | The `IsDescending(false, true, true)` index | The repository comment says "must mirror this expression exactly". A drift skips or repeats rows at page boundaries |
| `"notifications"` | `ATSLayout.CanAccessRoute`'s explicit allow | The `@page` route's last segment | §8.7 — renaming the page 302s every user to `/access-denied` |

### 10.6 Test coverage

| Suite | File | Covers |
|---|---|---|
| Unit | `Test/Test/BackendAPI/Modules/ATS.UnitTests/AtsNotificationServiceTests.cs` (471 lines) | Persist-then-push ordering, no-push-on-persist-failure, survives-push-failure, `Guid.Empty` no-op, truncation to 160/500, `RaiseForOrderAsync` addressing and both link shapes, the no-requestor and unknown-order cases, `RaiseForCompletedBulkEmailsAsync` wording and skipping, cursor emission |
| Integration | `Test/Test/BackendAPI/Modules/ATS.IntegrationTests/AtsHubGroupIsolationTests.cs` | The five group-assignment properties in §3.6 |
| Unit (caller) | `OMSTicketingProcessorServiceTests.cs`, `ReportServiceTests.cs`, `BulkEmailNotificationProcessorServiceTests.cs` | That each caller raises, via a `Mock<IAtsNotificationService>` |

The group-name assertion is the one that pins §10.5's first row:

```csharp
		// The group name has to be the canonical Guid string, because that is what
		// HubCallerContextExtensions.GetUserGroupName produces when the connection joins.
		_clients.Verify(x => x.Group(RecipientId.ToString()), Times.Once);
```

It verifies the push target against the same `ToString()` the hub uses, so it would not catch a
format change applied to both sides.

**Not covered:** no test exercises the four Carter endpoints or their gateway routes (there is no
ATS `PathIntegrationTests` equivalent to Auth's); nothing asserts that a `ReceiveNotification`
payload reaches a real client (§3.6); and no test covers the retention sweep.

---

## 11. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| `AtsNotificationType` (add/remove a value) | `ShareData/ATS/AtsNotificationTypes.cs`, `NotificationItem.TypeIcon`, `NotificationItem.AccentModifier`, `NotificationCenter.ShouldToast` | Four hand-synced switches across an assembly boundary; a missing icon degrades silently, a missing accent class renders unstyled (§1.4, §8.5) |
| A `.is-*` accent in `AccentModifier` | The `.ats-notif-icon.is-*` selectors in `wwwroot/css/ats.css` | Five classes, no shared enum; an unmatched one has no colour (§8.8) |
| `Title`/`Body`/`LinkUrl` column widths | `TitleMaxLength`/`BodyMaxLength`/`LinkUrlMaxLength` in `AtsNotificationService`, and a new migration | The constants are a copy of the config, not derived from it (§2.4, §10.5) |
| Anything that writes a `type` value | §9.11 | `Type` is the one field `Truncate` does not cover, against `varchar(60)` |
| `IATSClient.ReceiveNotification` | `NotificationService`'s `On<NotificationDTO>("ReceiveNotification", ...)` | The client subscribes by **string**; a rename compiles and breaks delivery (§10.5) |
| `ReceiveATSResponse` | `NewOrderComponent`, `EndorsementSubmissionService`, `AtsHubGroupIsolationTests.CaptureAtsResponses` | Three consumers, and the isolation tests assert through it — the only method they subscribe to (§3.6) |
| `ATSHub.OnConnectedAsync`'s group rule | All five `AtsHubGroupIsolationTests` facts, and `GetUserGroupName` | This is the whole authorisation story for the hub; there is no `[Authorize]` behind it (§3.2) |
| `GetUserGroupName`'s claim order or `ToString()` format | `AtsNotificationService.RaiseAsync`'s `.Group(recipientUserId.ToString())` | Two independent `ToString()` calls; a mismatch delivers nothing and saves the row (§10.5) |
| `SignalRHub:ATSBulkEndpoint` | `ATSPaths.cs:653` `MatchPath`, and the three UI hub-URL literals | The path is an env var on the server and a literal on the client, four times over (§3.4, §10.4) |
| The `GetBulkInsertResponseEntryPoint` route | Whether it still has **no** `Transforms` | Unlike every `/ats/*` route it forwards the path unchanged; adding a `PathSet` breaks the handshake (§3.4) |
| A Carter route string (`MapGet("getnotifications")`) | `PathSet` in `ATSPaths.cs` **and** the URL literal in `NotificationService` | Three literals in three assemblies (§10.5) |
| `MarkNotificationRead`/`MarkAllNotificationsRead`'s response shape | `SendAsync<bool>` / `SendAsync<int>` in `NotificationService` | Both return a **bare scalar**, unlike the two queries which return envelopes; wrapping them breaks deserialisation (§5.2) |
| `NotificationListDTO`'s properties | `UI/.../DTO/ATS/NotificationDTO.cs` | JSON binds by name; a mismatch yields default values, not an error (§10.5) |
| `KeysetPaginatedResult<TEntity>` | Its UI mirror in `DTO/SharedDTO/KeysetPaginatedResult.cs` | Two hand-copied classes with matching `TotalCount` semantics (§5.1) |
| `ApplyOrder` or `ApplySeek` | The other one, and `IsDescending(false, true, true)` in the configuration | The repository comment requires them to mirror each other exactly; drift skips or repeats rows at page boundaries (§1.2, §5.3) |
| `GetNotificationsPageAsync`'s `take` | `KeysetPage.Trim`'s `pageSize + 1` contract in `AtsNotificationService` | Trim assumes one extra row was fetched; removing it makes `HasMore` always false (§5.1) |
| `BuildOrderLink`'s destinations or search terms | The destination repository's search predicate, and `ModuleList.cs`'s `path` values | Ticketing searches first/last separately and needs the last name alone; `CanOpen` matches the last segment (§2.3, §8.7) |
| `AtsNotificationOptions` defaults | Whether an `AtsNotifications` section now exists in appsettings | None does today; the defaults are the shipped behaviour (§6) |
| `AtsNotificationRetentionService`'s sweep | `AtsAuditRetentionService` | Deliberately the same shape, including the sweep-on-boot (§6, §9.12) |
| `RaiseAsync`'s signature | All **six** call paths, including the two that call it directly | `BulkSubmissionProcessorService` and `BulkEmailNotificationProcessorService` do not go through `RaiseForOrderAsync` (§4.2, §4.5) |
| `AddAsync`'s `SaveChangesAsync` | Every caller's transaction boundary | It flushes the whole ambient context, not just the notification (§9.7) |
| `NotificationService.StartAsync` | §9.2, §9.3 | The `State == Connected` guard is what makes logout hand the socket to the next user, and what leaks a disconnected one |
| `AddScoped<INotificationService, ...>` | `NotificationCenter.Dispose` and `NotificationsPage.Dispose` | Scoped = app lifetime in WASM; both unsubscribes depend on it, and disposal never runs (§8.1, §8.5) |
| The `NotificationsChanged` argument convention | Both components' `OnNotificationsChangedAsync` | Non-null = arrival, null = count moved. Passing a non-null on a count change inserts a phantom row (§8.1) |
| `ModuleList.cs` (adding a notifications entry) | `ATSLayout.CanAccessRoute`'s explicit `"notifications"` allow | Adding the module makes the segment match, and every ungranted user is redirected (§8.7) |
| The `@page` route's last segment | `CanAccessRoute`, and `NotificationCenter.razor`'s "View all notifications" `href` | Three places hold `/s&i/ats/notifications` (§8.7) |
| `PreviewCount` (8) or `PageSize` (20) | `GetNotificationsQueryRequestValidator`'s 1..100 | Both are client-side constants the server re-clamps via `KeysetPage.Clamp` (§5.1, §8.5) |
| `notificationScroll.js`'s handle shape | `NotificationsPage.DisposeObserverAsync` | `dispose` is invoked by name on the returned object before the reference is disposed (§8.6, §9.9) |
| `RaiseAccountsExhaustedAsync` | §9.1, §9.10 | No cooldown, and a hand-rolled catch that can silence the fan-out entirely |
| `GetAtsAdministratorUserIdsAsync`'s cache tags | `RemoveByTagAsync(CacheTags.User)` in the add/edit-user, user-client and role decorators | The only cached read in the feature; the fan-out roster goes stale without them (§7) |
| `.ats-notif-row` / `.ats-notif-*` in `ats.css` | Both `NotificationCenter` and `NotificationsPage` | Shared by design so the two cannot drift; neither scoped sheet redeclares them (§8.8) |
