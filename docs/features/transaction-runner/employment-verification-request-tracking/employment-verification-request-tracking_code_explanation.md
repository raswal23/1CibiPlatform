# Employment Verification Request Tracking — Code Explanation

Companion to [`employment-verification-request-tracking.md`](employment-verification-request-tracking.md).
That document explains *what* the feature does and *why* the rules exist; this one is for a developer
about to change the code. It walks the real call chains, names the exact method at each hop, and quotes
what carries the correctness. Read it once, then use the closing table as a map.

> **Read §0 first.** The design doc describes roughly a third of this module. Everything below was
> verified against the code on branch `feature/Update-ReadMe-File`; where the two disagree, this
> document follows the code.

> **This module exposes an anonymous, unauthenticated write endpoint reachable from an emailed link.**
> §2 treats the token as the security-critical object it is; *Sharp edges* lists what that exposure
> currently costs. Nothing there is fixed — it is mapped so the next change does not make it worse.

---

## 0. Where the design doc no longer matches the code

| # | The design doc says | The code actually does |
|---|---|---|
| **C1** | "Public contracts" lists **two** gateway routes | `Path/EmploymentVerificationPaths.cs` declares **seven**, including `POST createrequest` and the anonymous `preview/{token}`, `verify/{token}`, `reject/{token}` |
| **C2** | Describes `Verified`/`Rejected` only as lifecycle outcomes | A whole second trust boundary exists — `Features/VerifyEmployment/`, three `AllowAnonymous()` slices — that the doc never names, routes or authorises on paper |
| **C3** | "Known gap: `getrequests` returns the entity including `VerificationTokenHash`" | True, and **`POST createrequest` does the same** (`Results.Ok(result)`, `result` being the entity). One leaking route is flagged; there are two |
| **C4** | "a bounced or unanswered request can be re-sent from the UI without a database edit" | **No re-send action exists.** `SendSelectedRequestAsync` is wired only inside the *Needs request* drawer (`EmploymentVerification.razor:357`). A released candidate reappears there and starting again inserts a **second row** |
| **C5** | Lifecycle: "Request row created before the email is attempted" | Correct, but the consequence is unstated: the row is **committed** first, so a failed send leaves a permanent `Pending` row, and `ListBlockedAtsSubjectIdsAsync` applies `Pending` no expiry — that candidate is blocked **forever** |
| **C6** | Tracking view shows "HR email" | `HrName` is **never assigned** on the create path, so it is NULL for every module-created request. `ResponseNotes` is a dead column — no writer, no reader, anywhere |
| **C7** | Candidate, employer, period, HR email come from ATS | When ATS has no dates the UI **fabricates them**: `?? DateTime.UtcNow.AddYears(-2)` / `?? DateTime.UtcNow.AddMonths(-6)`, plus `Position = "Not provided"` |
| **C8** | "`EmploymentVerificationPaths` is the only wiring, and that is correct" | True — but no EV route populates `RouteDefinitionDTO.Metadata`, so **none carries a `RateLimitPolicy`**. ATS's equivalent anonymous routes all do |
| **C9** | Guide §11a: token-link pages use `GenericLayout` | `VerifyEmployment.razor:1` **does** follow it. But `Layout/EmploymentVerificationLayout.razor` exists (itself `@layout GenericLayout`) and is referenced by **nothing** |
| **C10** | "Keep Employment Verification code vertically structured … do not compress into one-line blocks" | `EmploymentVerification.csproj:2` packs the whole `PropertyGroup` onto one line; `EmploymentVerificationDbContext.cs` has no blank line between `Requests` and `OnModelCreating` |

Verified **correct** in the design doc: the availability table matches `ListBlockedAtsSubjectIdsAsync`
predicate-for-predicate; `Expired` is declared and never assigned; `asOfUtc` is a parameter, not a clock
read in the repository; `MarkRespondedAsync` gates on `Pending || Sent`; the decorator invalidates
inside each write gated on the returned bool; `SentVerificationRequestDTO` omits the hash;
`ResponseRate` returns an em dash on an empty list.

---

## 1. The data model

### 1.1 Entity — `Data/Entities/EmploymentVerificationRequest.cs`

The whole file; the status enum sits above the class in the same file.

```csharp
public enum VerificationRequestStatus { Pending, Sent, Verified, Rejected, Expired }
public sealed class EmploymentVerificationRequest
{
	public Guid Id { get; set; }
	public Guid? AtsSubjectId { get; set; }
	public string CandidateName { get; set; } = "";
	public string PreviousEmployer { get; set; } = "";
	public string Position { get; set; } = "";
	public DateTime? EmploymentStartDate { get; set; }
	public DateTime? EmploymentEndDate { get; set; }
	public string? HrName { get; set; }
	public string HrEmail { get; set; } = "";
	public VerificationRequestStatus Status { get; set; } = VerificationRequestStatus.Pending;
	public string VerificationTokenHash { get; set; } = "";
	public DateTime TokenExpiresAt { get; set; }
	public DateTime RequestedAt { get; set; }
	public DateTime? SentAt { get; set; }
	public DateTime? VerifiedAt { get; set; }
	public DateTime? RejectedAt { get; set; }
	public string? ResponseNotes { get; set; }
}
```

`AtsSubjectId` is nullable and **not** a foreign key — a soft pointer at
`EmailInvitationRequest.EmailInvitationID` in the ATS schema, unenforced by the model. Deleting an ATS
order leaves the request pointing at nothing.

### 1.2 EF configuration — `Data/EntityConfiguration/EmploymentVerificationRequestConfiguration.cs`

Picked up by `ApplyConfigurationsFromAssembly(typeof(EmploymentVerificationDbContext).Assembly)`;
nothing is configured inline in the context. Exactly two indexes:

```csharp
		builder.Property(request => request.Status)
			.HasConversion<string>()
			.HasMaxLength(20);

		builder.HasIndex(request => request.VerificationTokenHash)
			.IsUnique();

		builder.HasIndex(request => new
		{
			request.Status,
			request.RequestedAt
		});
```

The **unique index on `VerificationTokenHash` is what makes `SingleOrDefaultAsync` safe** (§2.2).
`HasConversion<string>()` means the column holds `"Sent"`, not `1`, so renaming an enum member is a
data migration. Length caps (`CandidateName` 200, `PreviousEmployer` 250, `Position` 200, `HrEmail`
320, `VerificationTokenHash` 128) are mirrored exactly by `CreateRequestCommandValidator` — a mismatch
would surface as a `DbUpdateException` instead of a 400.

### 1.3 Migration — `BackendAPI/API/APIs/Migrations/EmploymentVerification/20260814052157_InitialEmploymentVerification.cs`

One migration, purely additive: `EnsureSchema("employment_verification")`, one `CreateTable`, two
`CreateIndex`; `Down()` drops the table. The **namespace does not match the folder**:

```csharp
namespace EmploymentVerification.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialEmploymentVerification : Migration
```

ATS migrations under `Migrations/ATS/` use `APIs.Migrations.ATS`. This works — EF discovers migrations
by assembly (`MigrationsAssembly("APIs")`) and attribute, not namespace — but a
`dotnet ef migrations add` run from the wrong startup project will not find it.

---

## 2. The token — the security-critical core

### 2.1 Generation, and the link that is actually emailed

`Services/EmailVerification/EmploymentVerificationService.cs:85-90`, then line 108:

```csharp
		var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
			.Replace("+", "-")
			.Replace("/", "_")
			.TrimEnd('=');
		var now = DateTime.UtcNow;
		var hashToken = _hashService.Hash(token);
```

```csharp
		var verificationLink = $"{_applicationformBaseUrl}/{hashToken}";
```

**Read those together — the second is the whole story: the link carries `hashToken`, not `token`.** The
256-bit random value is generated, hashed, and then **discarded**; `token` is never emailed, stored or
returned. What the HR recipient clicks is `SHA512(random)` as unpadded base64url, which is *also* the
exact string in `EmploymentVerificationRequests.VerificationTokenHash`. Answering plainly:

- **Plaintext or hashed?** The column holds a SHA-512 digest — but the digest *is* the bearer
  credential. There is no secret the database does not hold, so a read of that column by any route
  yields a working link. Hashing buys nothing against database or API disclosure; it only keeps the raw
  32 bytes out of the URL. The column name actively misleads a reader into assuming otherwise.
- **Entropy:** 256 bits from `RandomNumberGenerator` — ample. Guessing is not the threat. **Shared
  generator used? No.** `ISecureToken`/`SecureToken`
  (`BuildingBlocks/SharedServices/Implementations/SecureToken.cs`) does exactly this — `GetBytes(32)`
  then base64url — and ATS and Auth both use it; this module inlines a copy. It *does* take `IHashService`.
- **Expiry window:** `_configuration.GetSection("EmailVerification").GetValue<int>("TokenExpiryInHours", 72)`
  (line 26), applied as `TokenExpiresAt = now.AddHours(_tokenExpiryHours)`. Default 72h.
- **Second use:** the token is **not destroyed** — see §2.3.

### 2.2 Validation — `Features/VerifyEmployment/Query/GetRequestByToken`

The only shape check, quoted in full from `GetRequestByTokenHandler.cs`:

```csharp
	// The emailed link carries the stored SHA-512 hash from IHashService, rendered as
	// unpadded base64url: 86 characters. Rejecting anything else keeps malformed links
	// out of the database lookup.
	private const int TokenLength = 86;

	public GetRequestByTokenQueryValidator()
	{
		RuleFor(query => query.Token)
			.NotEmpty()
			.WithMessage("A verification token is required.")
			.Length(TokenLength)
			.WithMessage("The verification token is malformed.")
			.Matches("^[A-Za-z0-9_-]+$")
			.WithMessage("The verification token is malformed.");
	}
```

`VerifyRequestCommandValidator` and `RejectRequestCommandValidator` are **byte-identical copies** in
their own handler files — three copies of `TokenLength = 86`. The lookup,
`Data/Repository/EmploymentVerificationRepository.cs:26-31`:

```csharp
	public Task<EmploymentVerificationRequest?> FindByTokenHashAsync(
		string tokenHash,
		CancellationToken cancellationToken) =>
		db.Requests.SingleOrDefaultAsync(
			request => request.VerificationTokenHash == tokenHash,
			cancellationToken);
```

**Constant time? No.** A SQL `=` against a unique b-tree index, executed by PostgreSQL — not
`CryptographicOperations.FixedTimeEquals`. The service comment states the intent: *"The emailed link
carries the stored hash itself, so it is matched directly here and in `GetPreviewByTokenAsync` without
re-hashing."* `IHashService.Verify` exists and **does** use `FixedTimeEquals`; this module never calls
it. Practical exploitability is low (86 characters, 256 bits, timing measured across a network and a
query planner), but the constant-time verifier is sitting there unused.

Then the disclosure gate in `GetPreviewByTokenAsync`, whose ordering is deliberate and correct —
`Expired()` and `NotFound()` both carry `Request = null`, so a lapsed or unknown token leaks nothing
about the candidate:

```csharp
		// Check the token before exposing any request details: an expired or spent
		// link must not disclose the candidate or employer to the caller.
		if (entity.TokenExpiresAt < DateTime.UtcNow)
		{
			return EmploymentVerificationPreviewResult.Expired();
		}
```

The three outcomes are however **distinguishable** — 404 `TokenNotFound`, 410 `TokenExpired`, 409
`TokenAlreadyUsed` — which makes the endpoint a validity oracle for a token you already partially hold.

### 2.3 Single use, and the race

`VerifyAsync` checks the terminal status, then relies on the update predicate:

```csharp
		// Single use is enforced by the terminal status, not by destroying the
		// hash: the row must stay findable so a second click can be told the
		// link was already answered instead of being reported as unknown.
		if (entity.Status is VerificationRequestStatus.Verified
			or VerificationRequestStatus.Rejected)
		{
			return EmploymentVerificationCompletionResult.AlreadyCompleted(
				EmploymentVerificationPreviewDTO.FromEntity(entity));
		}
```

and `MarkRespondedAsync` — the load-bearing lines of the module:

```csharp
		// Single use is enforced here rather than by the prior read: restricting
		// the update to a non-terminal row means two simultaneous clicks cannot
		// both record a response.
		var affectedRows = await db.Requests
			.Where(request => request.Id == id)
			.Where(request =>
				request.Status == VerificationRequestStatus.Pending ||
				request.Status == VerificationRequestStatus.Sent)
			.ExecuteUpdateAsync(
				setters => setters
					.SetProperty(request => request.Status, status)
					.SetProperty(request => request.VerifiedAt, verifiedAt)
					.SetProperty(request => request.RejectedAt, rejectedAt),
				cancellationToken);

		return affectedRows > 0;
```

Two simultaneous clicks: one update matches a non-terminal row and returns 1; the other finds the row
already terminal and returns 0, which `VerifyAsync` maps to `AlreadyCompleted` → HTTP 409. First
response wins, deterministically, with no transaction and no explicit lock. The predicate accepts
`Pending` too, so a row whose email never went out (C5) is still answerable by anyone who obtains the
link. And because the hash is deliberately never destroyed, **the token stays a valid lookup key for
the life of the row** — there is no purge job and no revocation.

### 2.4 Where the token travels

**Path segment, not query string** — `api/employment-verification/preview/{token}`, `.../verify/{token}`,
`.../reject/{token}`. The token never follows a `?`, so it is not picked up by analytics that record
query strings separately, nor reordered by client-side query builders. It is not free: a path token
still lands in YARP and Kestrel access logs, in any intermediary proxy log, and in browser history
exactly as a query token would. Referer leakage is the one thing genuinely avoided —
`VerifyEmployment.razor` has **no outbound `<a href>`**, so nothing navigates off the anonymous page
carrying the URL. The client escapes with `Uri.EscapeDataString(token)` on both GET and POST, a no-op
for the base64url alphabet but correct if the validator's character class is ever widened.

Gateway transform detail that is easy to break:

```csharp
            // PathPattern (not PathSet) substitutes the {token} route value.
            // PathSet forwards the literal text "{token}" to the backend.
            Transforms: new Dictionary<string, string>
			{
				["PathPattern"] = "/api/employment-verification/preview/{token}"
			}),
```

The three token routes use `PathPattern`; the four parameterless routes use `PathSet`. Swapping one for
the other on a token route forwards the literal `{token}` and every link 404s.

---

## 3. One request traced end to end — `Command/CreateRequest`

**Frontend.** `EmploymentVerification.razor:357` wires the drawer's send button to
`SendSelectedRequestAsync` in `EmploymentVerification.razor.cs`. It refuses without an HR email, then
builds the transport DTO — including the fabricated fallbacks from C7:

```csharp
				Position = string.IsNullOrWhiteSpace(SelectedCandidate.Position)
					? "Not provided"
					: SelectedCandidate.Position,
				HrEmail = SelectedCandidate.HrEmail, //"contract.fullstackdev@cibi.com.ph",
				EmploymentStartDate = ToDateTime(SelectedCandidate.StartDate)
					?? DateTime.UtcNow.AddYears(-2),
				EmploymentEndDate = ToDateTime(SelectedCandidate.EndDate)
					?? DateTime.UtcNow.AddMonths(-6)
```

The UI service's `CreateAndSendAsync` posts to `employmentverification/createrequest` on the named
`"API"` client and binds the body to `EmploymentVerificationResponseDetailsDTO`, which declares only
`CandidateName`. The gateway route `CreateEmploymentVerificationRequest` maps that to
`PathSet` `/api/employment-verification/requests`; with no `Metadata`, the gateway's global limiter
falls to its `_` arm.

**Endpoint.** `Features/VerificationRequests/Command/CreateRequest/CreateRequestEndpoint.cs`:

```csharp
		app.MapPost(
				"api/employment-verification/requests",
				async (
					CreateEmploymentVerificationRequest request,
					ISender sender,
					CancellationToken cancellationToken) =>
				{
					var command = new CreateRequestCommand(request);
					var result = await sender.Send(command, cancellationToken);

					return Results.Ok(result);
				})
			.RequireAuthorization()
			.WithTags("Employment Verification");
```

`Results.Ok(result)` returns the **entity**, hash included (C3).

**Validation, before the handler.** `AddEmploymentVerificationMediaTR` registers `ValidationBehavior<,>`
then `LoggingBehavior<,>` as open behaviors, so `CreateRequestCommandValidator` runs first. Its rules
mirror the EF caps from §1.2 plus one cross-field rule:

```csharp
		RuleFor(command => command.Request.EmploymentEndDate)
			.GreaterThanOrEqualTo(command => command.Request.EmploymentStartDate)
			.When(command =>
				command.Request.EmploymentStartDate.HasValue &&
				command.Request.EmploymentEndDate.HasValue)
			.WithMessage("Employment end date must be on or after the start date.");
```

**Handler → service → repository.** `CreateRequestHandler.Handle` is a one-liner delegating to
`service.CreateAndSendAsync(request.Request, cancellationToken)`. That method runs, in order: a
defensive `IsNullOrWhiteSpace` re-check that duplicates the validator and throws a bare
`ArgumentException`; token generation (§2.1); entity construction with `Id = Guid.NewGuid()` — **not**
`Guid.CreateVersion7()`, unlike ATS — and `TokenExpiresAt = now.AddHours(_tokenExpiryHours)`;
`AddAsync`; the inline HTML body; the send; then `MarkSentAsync`. `AddAsync` is
`db.Requests.AddAsync(...)` + `SaveChangesAsync` + `return true`, wrapped by the decorator, which
revokes `RequestsTag` on success. `Status` is still `Pending` at that point — **the row is committed
before the email is attempted.** Then:

```csharp
		if (!await _emailService.SendEmailAsync(
				entity.HrEmail,
				"Employment verification request",
				body,
				true))
		{
			throw new InvalidOperationException("The verification email could not be sent.");
		}

		var sentAt = DateTime.UtcNow;
		await _repository.MarkSentAsync(entity.Id, sentAt, cancellationToken);
```

A `false` return throws out of the handler, past `CustomExceptionHandler`, and the caller sees a 500 —
but the `Pending` row survives, permanently blocking that candidate (C5). Only on success does
`MarkSentAsync` flip the status and stamp `SentAt`, again through the decorator, again revoking the tag.
The method then mutates the local `entity` to match and returns it. Back in the UI, `LoadAsync()`
refetches both lists: the candidate has left *Needs request* and appears under *Tracking*.

**The email body** is one interpolated raw string literal inside the service (lines 109-136), not a
template file: applicant, previous employer, position and period in a styled table, one CTA anchor at
`verificationLink`, and this line —

```html
				  <p style='font-size:15px;line-height:1.6'>Choose one response below. This secure link can be used once and expires in 72 hours.</p>
```

The `72` is **hardcoded prose** while `_tokenExpiryHours` is configuration; set `TokenExpiryInHours` to
24 and the email still promises 72. There is no rejection link either — the recipient is told to "open
the link and choose the rejection option", so both actions live on the page.

---

## 4. The remaining six slices, as diffs

All seven share the plumbing: Carter `ICarterModule` → `ISender` → MediatR handler →
`IEmploymentVerificationService` → decorated repository → `EmploymentVerificationDbContext`. Only what
differs is listed.

| Slice | Backend route | Gateway route | Auth | Rate limit | Diff from §3 |
|---|---|---|---|---|---|
| `VerificationRequests/Command/CreateRequest` | `POST …/requests` | `/employmentverification/createrequest` | `.RequireAuthorization()` | **none** | traced in §3 |
| `VerificationRequests/Query/GetRequests` | `GET …/requests` | `/employmentverification/getrequests` | `.RequireAuthorization()` | **none** | No validator, no `WithName`, no `Produces`. Returns `IReadOnlyList<EmploymentVerificationRequest>` — **entities, hash included**. Nothing in the UI calls it |
| `VerificationRequests/Query/GetSentRequests` | `GET …/requests/sent` | `/employmentverification/getsentrequests` | `.RequireAuthorization()` | **none** | Same shape but projects through `SentVerificationRequestDTO.FromEntity`, which omits the hash. Fully documented with `WithName`/`Produces`/`WithDescription` |
| `VerificationRequests/Query/GetAvailableATSRecords` | `GET …/ats/in-progress` | `/employmentverification/getatsinprogress` | `.RequireAuthorization()` | **none** | The only slice leaving the module (§5). Returns `ATSInProgressEmploymentRecord` — an **ATS-owned type** — straight over the wire |
| `VerifyEmployment/Query/GetRequestByToken` | `GET …/preview/{token}` | `/employmentverification/preview/{token}` | **`.AllowAnonymous()`** | **none** | Token validator (§2.2). Maps `PreviewTokenStatus` → 200/410/409/404 via `Results.Problem(title: …)`. Read-only; never mutates |
| `VerifyEmployment/Command/VerifyRequest` | `POST …/verify/{token}` | `/employmentverification/verify/{token}` | **`.AllowAnonymous()`** | **none** | `service.VerifyAsync(request.Token, reject: false, …)` → `CompletionStatus` switch, same codes and titles |
| `VerifyEmployment/Command/RejectRequest` | `POST …/reject/{token}` | `/employmentverification/reject/{token}` | **`.AllowAnonymous()`** | **none** | Byte-for-byte VerifyRequest except the route and `reject: true`. Both writes take **no body** — `PostAsync(..., content: null, ...)` |

Two structural notes. **One service method backs both write commands** — `VerifyAsync(token, reject, ct)`
carries the flag, so a fix to response recording lands in one place (the guide's "reuse the service, do
not fork it"). **`AllowAnonymous()` is doing real work, not just omitting a policy:** the composition
root calls `services.AddAuthorization()` (`APIs/ServiceConfig/ServiceConfiguration.cs:263`) with **no
policies defined at all**, so `.RequireAuthorization()` on the four staff slices means "any
authenticated platform user" and nothing more. `[RequirePermission(8, 9)]` lives only in Blazor,
enforced by `SecurePageBase.OnInitializedAsync` — a UI guard, not an API authorisation rule.

---

## 5. The cross-module read — `GetAvailableATSRecords`

The only place the module reaches into another module's data, and the coupling is invisible from the
EmploymentVerification folder alone. **The contract** is ATS-owned,
`BackendAPI/Modules/ATS/Shared/Contracts/IATSVerificationDataProvider.cs`:

```csharp
public sealed record ATSInProgressEmploymentRecord(
	Guid SubjectId,
	string CandidateName,
	string Employer,
	string? Position,
	DateOnly? StartDate,
	DateOnly? EndDate,
	string? HrName,
	string? HrEmail);

public interface IATSVerificationDataProvider
{
	Task<IReadOnlyList<ATSInProgressEmploymentRecord>> GetInProgressEmploymentAsync(CancellationToken cancellationToken = default);
}
```

A narrow contract in `Shared/Contracts/` is the right shape. But the module also takes a **direct
compile-time reference** — `EmploymentVerification.csproj` has
`<ProjectReference Include="..\ATS\ATS.csproj" />`, needed for this contract *and* for `ATSEmailService`
(§6). EmploymentVerification cannot build without ATS.

**The implementation**, `ATS/Shared/Implementations/ATSVerificationDataProvider.cs` (registered by
`ATSServiceConfiguration.cs:113`), takes `ATSDBContext` and runs one three-way join:

```csharp
		return await (
			from user in db.UserDetails.AsNoTracking()
			join invitation in db.EmailInvitationRequests.AsNoTracking()
				on user.UserId equals invitation.RequestorId
			join employment in db.ProfessionalExperiences.AsNoTracking()
				on invitation.EmailInvitationID equals employment.EmailInvitationID
			where invitation.OrderStatus == "In Progress"
			select new ATSInProgressEmploymentRecord(
```

**Is the caller's ATS scope applied? No.** No `IAtsAccessScopeResolver` parameter, no `ClientId`
predicate, no owner filter — the only `where` is the literal `OrderStatus == "In Progress"`. This is
**not** the ladder ATS uses elsewhere: `ATS/Services/AccessScope/IAtsAccessScopeResolver.cs` returns
`AtsAccessScope(AuthorizedClientIds, RequiredOwnerId)` where `null` means "may not read ATS at all" and
an empty collection means "sees nothing". EmploymentVerification neither calls it nor implements an
equivalent. Any authenticated platform user gets **every in-progress ATS candidate across every client**.

Three consequences are load-bearing here. **`HrEmail` is not an HR contact** — it is `user.UserEmail`
joined on `user.UserId equals invitation.RequestorId`, the platform account of whoever *raised the ATS
order*, i.e. a CIBI staff member, while `HrName` maps from `employment.Emp1SupervisorName`.
**`SubjectId` is `invitation.EmailInvitationID`**, so `AtsSubjectId` points at the *order*, not a
person, and the blocking rule keys on that. **The `UserDetails` join can fan out** — its key is
composite `(UserId, ModuleId)`, one row per module grant, so a requestor with three grants yields three
joined rows; the trailing `.Distinct()` rescues this only because `UserEmail` is identical across grants
and the projection is therefore equal. Add any per-grant column and the duplicates return.

**The blocking filter then runs in this module**, in `GetAvailableATSRecordsAsync`: fetch all ATS
records, short-circuit if empty, call `ListBlockedAtsSubjectIdsAsync(DateTime.UtcNow, …)`, short-circuit
if nothing is blocked, else `ToHashSet()` and filter. The availability rule is business logic in the
service, as the design doc claims; the repository only projects ids. The two clock reads — here and
inside the expiry checks — are independent, but the predicates (`TokenExpiresAt >= asOfUtc` blocks;
`TokenExpiresAt < UtcNow` expires) are exact complements, so no row can both block and serve.

---

## 6. The keyed email service

The module owns no SMTP sender; it borrows ATS's, keyed. Registration,
`ServiceConfig/EmploymentVerificationServiceConfiguration.cs:57`:

```csharp
		services.AddKeyedScoped<IEmailService, ATSEmailService>("ats");
```

Resolution, `EmploymentVerificationService.cs:16` — a constructor attribute, not a manual
`GetKeyedService`:

```csharp
		[FromKeyedServices("ats")] IEmailService emailService,
```

The `"ats"` string must agree across those two files with nothing enforcing it at compile time; a typo
fails at first resolution, not at build. It is registered a **second time** by
`ATS/ServiceConfig/ATSServiceConfiguration.cs:148` with the identical key and type, and both run inside
`AddModuleServices` — harmless today (last wins, same implementation) but it makes deleting one look
safe when it is not obviously so. Auth registers a *different* sender under `"auth"`, which is why the
key exists at all. What the sender does — `ATSEmailService.SendEmailAsync` is a thin adapter over the
ATS account pool:

```csharp
	public Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
	{
		return SendATSEmailAsync(toEmail, subject, body);
	}
```

`isHtml` is discarded — the service passes `true` and the ATS path is always HTML. The return is
`result.IsSent` from `SendATSEmailWithResultAsync`, which walks registered SMTP accounts and moves on
when one is capped or throttled. That adapter is why `CreateAndSendAsync` can only treat `false` as "not
sent" and cannot tell a rate limit from a bad address.

---

## 7. The cache decorator

`Data/Cache/EmploymentVerificationCacheRepository.cs`, applied by Scrutor:

```csharp
		// Scrutor decorates the concrete repository with the HybridCache behavior.
		services.AddScoped<
			IEmploymentVerificationRepository,
			EmploymentVerificationRepository>();
		services.Decorate<
			IEmploymentVerificationRepository,
			EmploymentVerificationCacheRepository>();
```

`RequestsKey` and `RequestsTag` are the **same literal**, `"employmentverification:requests"`. Only
`ListAsync` is cached — two minutes, tagged. All three writes call
`cache.RemoveByTagAsync(RequestsTag, cancellationToken)` gated on the repository's returned bool, so a
`MarkRespondedAsync` that lost the race (`affectedRows == 0`) does **not** invalidate. Correct: nothing
changed.

**Is the token lookup cached? No — and that is the important answer.**

```csharp
	public Task<EmploymentVerificationRequest?> FindByTokenHashAsync(
		string tokenHash,
		CancellationToken cancellationToken) =>
		repository.FindByTokenHashAsync(tokenHash, cancellationToken);
```

A straight pass-through, as is `ListBlockedAtsSubjectIdsAsync`, with the reason inline:

```csharp
	// Deliberately uncached: the result turns on how the supplied instant compares
	// to each token expiry, so a cached list would keep lapsed requests blocking
	// their candidate until the entry aged out.
```

Both are right. Caching `FindByTokenHashAsync` would be a genuine bug: a spent token could still be
served a `Valid` preview from cache for up to two minutes, defeating the single-use guarantee §2.3 rests
entirely on the database for. **Any new cache entry here must keep that method a pass-through.** The
two-minute TTL is also why a manual database edit can show stale tracking data for up to two minutes —
the normal response path revokes the tag itself.

---

## 8. The frontend

Two pages, two audiences, two layouts. **Staff page** —
`Pages/EmploymentVerification/EmploymentVerification.razor` (367 lines) with `.razor.cs` and scoped
`.razor.css`:

```razor
@page "/employmentverification/verification"
@layout ConsoleLayout
@inherits SecurePageBase
@attribute [RequirePermission(8, 9)]
```

`OnInitializedAsync` calls `await base.OnInitializedAsync();` then returns early on `!IsPageAuthorized`
before `LoadAsync()` — the ordering the design doc warns about, done correctly. One segmented switcher
(`NeedsRequestView`/`TrackingView`), each with its own search term so a filter does not carry across,
both lists held in memory and filtered client-side. `GetDisplayStatus` renders
`Sent && TokenExpiresAt < UtcNow` as `"Expired"` for display only; `ResponseRate` returns `"—"` on an
empty list. The drawer moves focus on render and closes on Escape but deliberately not on backdrop
click. All module CSS is `ev-`-prefixed.

**Anonymous page** — `Pages/EmploymentVerification/VerifyEmployment.razor`, first line
`@layout GenericLayout`, which is what guide §11a requires of a token-link page. It declares **two routes
on one component**, so `[Parameter] public string Token { get; set; } = ""` binds from either:

```razor
@page "/employmentverification/verify/{Token}"
@page "/employmentverification/reject/{Token}"
```

`OnParametersSetAsync` → `LoadPreviewAsync()` → `VerificationService.GetPreviewAsync(Token, _cancellation.Token)`
(the GET from §4), mapping `VerificationLinkFailure` onto four mutually exclusive render states: expired,
already-used, generic error (`NotFound`/`Unknown`), and the actionable confirmation view. Both buttons
funnel into `CompleteAsync(reject, action)` and disable via `_isSubmitting`. The page is `IDisposable`,
cancels its own `CancellationTokenSource`, and swallows `OperationCanceledException` in both paths.

**The error-title contract is the fragile part.** `ReadFailureAsync` switches on the ProblemDetails
`title` string:

```csharp
		var failure = error?.Title switch
		{
			"TokenExpired" => VerificationLinkFailure.Expired,
			"TokenAlreadyUsed" => VerificationLinkFailure.AlreadyUsed,
			"TokenNotFound" => VerificationLinkFailure.NotFound,
			_ => VerificationLinkFailure.Unknown
		};
```

Those literals are typed independently in three backend endpoints. Rename one `title:` and the anonymous
page silently degrades to its generic error state — no compile error, no test to catch it (§9).
Deserialization needs `PropertyNameCaseInsensitive = true` because ProblemDetails is emitted camelCase;
the file's own comment records that `title`/`detail` otherwise bind to null and raw JSON leaks into the
page.

**`Layout/EmploymentVerificationLayout.razor` is dead** — a `@layout GenericLayout` wrapper around
`<div class="employment-verification-layout">@Body</div>`, referenced by nothing in `UI/`.
`VerifyEmployment.razor` uses `GenericLayout` directly, so the module *does* follow the guide's rule;
delete the layout or use it, because as it stands it invites a future page to adopt one no page has
exercised.

---

## 9. Tests

**There are none.** `Test/Test/BackendAPI/Modules/` contains `ATS.UnitTests`, `ATS.IntegrationTests`,
`Auth.UnitTests`, `Auth.IntegrationTests`, `PhilSys.UnitTests`, `PhilSys.IntegrationTests` and
`OMS.UnitTests`; there is no `EmploymentVerification.*` project, and a glob for
`Test/**/*EmploymentVerification*` returns nothing.

So the module's most sensitive code is uncovered: token generation and expiry arithmetic, the three
copies of the 86-character validator, the `MarkRespondedAsync` predicate that is the *only* thing
enforcing single use, the availability rule the design doc calls business logic belonging in the
service, and all three anonymous endpoints. That rule was designed to be testable — `asOfUtc` is a
parameter precisely so the instant can be supplied — and no test supplies it.

---

## Sharp edges

Ordered by how much they matter. **Findings, not fixes.**

**S1 — The stored column *is* the bearer credential (§2.1).** `verificationLink` interpolates
`hashToken`, the same string written to `VerificationTokenHash`; the 256-bit random value is discarded.
Any read of that column yields a working anonymous link, so hashing gives no protection against
disclosure of the database or of an API response. The column name misleads a reader into assuming the
opposite.

**S2 — `GET /api/employment-verification/requests` hands every live token to any logged-in user.**
`GetRequestsHandler` returns the entity, `VerificationTokenHash` included, behind a bare
`.RequireAuthorization()`. Because `AddAuthorization()` defines no policies and `[RequirePermission(8, 9)]`
is Blazor-only (§4), *any* authenticated account in *any* module can enumerate every live link and then
answer them anonymously via `POST verify/{token}`. The design doc flags this as a "known gap" to fix
before relying on the route; the route is live and reachable through the gateway today. With S1 this is
a complete compromise of the verification outcome, and the top priority here.

**S3 — `POST /api/employment-verification/requests` also returns the hash** in `Results.Ok(result)`
(§3). Narrower than S2 — only the creator's own new token — but it puts a live credential into an HTTP
response body, browser network logs and any response-caching layer for no benefit: the UI binds it to a
DTO declaring only `CandidateName`.

**S4 — No rate limit on the three anonymous routes.** `EmploymentVerificationPaths` never populates
`Metadata`, so `GatewayServiceExtensions.AddRateLimiting` falls through to its `_` arm: **500
requests/second**, partitioned by the constant string `"default"` rather than by client IP — one bucket
shared with every other untagged gateway route. ATS's anonymous candidate routes use
`RateLimitPolicies.AnonymousApplicationForm`: 30/min keyed on `RemoteIpAddress`, with a comment
explaining that per-IP partitioning is what makes enumeration impractical. Same threat shape, none of
the protection. The backend `APIs` project registers no rate limiter at all, so anything reaching it
without the gateway is unbounded. Entropy makes blind guessing futile regardless; the missing bound
matters for denial of service, for brute force against a *partially* known token, and because it breaks
a stated platform rule.

**S5 — Token comparison is not constant time (§2.2).** `SingleOrDefaultAsync` on a SQL `=` against a
unique index, while `IHashService.Verify` — which uses `CryptographicOperations.FixedTimeEquals` — is
already available to this module and unused. Low practical risk at this entropy over a network, but a
deviation from the platform's own verifier with no recorded reason.

**S6 — Spent and expired tokens stay valid lookup keys and answer distinguishably.** The hash is
deliberately never destroyed (§2.3), so `GET preview/{token}` returns 409 `TokenAlreadyUsed` on a used
link and 410 `TokenExpired` on a lapsed one, versus 404 `TokenNotFound` — a validity oracle confirming
that a specific token is real and whether it was answered. It is also good UX and the defending comment
is sound, so this is a trade-off to know about rather than an obvious defect. There is no purge, no
revocation, and no way to kill a leaked link short of a database edit.

**S7 — The anonymous preview discloses more than the page needs.** `EmploymentVerificationPreviewDTO`
carries `SubjectId` (the ATS `EmailInvitationID`), `HrEmail`, `RequestedAt`, `SentAt` and
`TokenExpiresAt`; the page renders only applicant, employer, position, period and expiry. An
unauthenticated caller holding a link learns an internal ATS order id — a usable handle against other
ATS endpoints.

**S8 — Nothing binds the response to the intended recipient.** The link is the only credential: no email
confirmation, no OTP, no check that the caller is `HrEmail`. Inherent to emailed links and true of the
ATS application form too — but here the result is a *recorded attestation about a named person's
employment history*, a stronger claim than filling in a form. S2/S3 widen it from "anyone who gets the
email" to "anyone with a platform login".

**S9 — The "HR contact" is usually a CIBI staff member (§5).** `HrEmail` is `UserDetails.UserEmail` for
the order's `RequestorId`, so the module asks that person to confirm or deny a former employee's job
title and dates. If the intent is to reach the *previous employer's* HR, this query does not do it; if
the intent is internal confirmation, the email copy ("A former employee listed you as an HR contact",
"…you may disregard this message") is wrong. Either way mapping and wording disagree.

**S10 — The cross-module ATS read is entirely unscoped (§5).** No client or owner filter, so the staff
page lists every in-progress ATS candidate platform-wide. Every other ATS read path goes through
`IAtsAccessScopeResolver`; this one does not, and implements no equivalent.

**S11 — A failed send orphans a `Pending` row that blocks the candidate forever (§3, C5).** The row
commits before the send; a `false` return throws and leaves `Pending` behind, and
`ListBlockedAtsSubjectIdsAsync` applies `Pending` no expiry. No sweeper, no retry, no UI action clears
it. The candidate simply stops appearing in *Needs request* with no visible reason.

**S12 — The UI fabricates employment dates and positions (§3).** `?? UtcNow.AddYears(-2)` and
`?? UtcNow.AddMonths(-6)` invent a plausible-looking period, and `"Not provided"` is sent as the literal
position. The recipient is asked to attest the information is "accurate to the best of your knowledge",
and a `Verified` response is recorded against invented data. The most likely source of silently wrong
records in this module.

**S13 — The email hardcodes "expires in 72 hours"** while `_tokenExpiryHours` comes from
`EmailVerification:TokenExpiryInHours` (§3); they diverge the first time anyone changes the setting.
Related: `_applicationformBaseUrl` defaults to `string.Empty` when `EmploymentVerificationUrl` is unset,
producing `href='/{hash}'` — a relative link in an email, resolving against whatever origin the
recipient's client assumes. Neither value is validated at startup.

**S14 — Zero test coverage (§9)** on an anonymous write path whose single-use guarantee rests on one
`Where` clause.

**S15 — Dead code that will mislead the next reader.** `CreateApiExceptionAsync` in the UI service is
private and never called; `Layout/EmploymentVerificationLayout.razor` is referenced by nothing (§8);
`AddEmploymentVerificationCarterModules` is never called, because the composition root's
`AddModuleCarter` already lists `_employmentVerificationAssembly` in its `DependencyContextAssemblyCatalog`;
`ResponseNotes` has no reader or writer; `HrName` is never set on the create path; and
`EmploymentVerification.razor.cs` still carries debug comments naming a real internal address —
`HrEmail = SelectedCandidate.HrEmail, //"contract.fullstackdev@cibi.com.ph",`.

**S16 — The 86-character rule is duplicated three times (§2.2)** with no shared constant. Changing
`HashService`'s output length breaks all three validators independently, and the failure mode is a 400
on every link rather than a build error.

---

## Wiring

Everything that must agree across files with nothing enforcing it at compile time.

| Thing | Declared in | Must agree with |
|---|---|---|
| Gateway routes | `EmploymentVerificationPaths.GetRoutes()` — 7 `RouteDefinitionDTO`s | Loaded via `IReverseProxyModule` discovery. The `ReverseProxy:Routes` appsettings section has no reader; do not add entries there |
| Backend route strings | Each `*Endpoint.cs` `MapGet`/`MapPost` literal | The `PathSet`/`PathPattern` value in `EmploymentVerificationPaths` — four `PathSet`, three `PathPattern` (§2.4) |
| Keyed email sender | `"ats"` at `EmploymentVerificationServiceConfiguration.cs:57` | `[FromKeyedServices("ats")]` at `EmploymentVerificationService.cs:16`; also registered at `ATSServiceConfiguration.cs:148` |
| Token length | `TokenLength = 86` × 3 validators | `HashService.Hash` output width (SHA-512 → 86 unpadded base64url chars) |
| ProblemDetails titles | `"TokenExpired"` / `"TokenAlreadyUsed"` / `"TokenNotFound"` in 3 endpoints | `ReadFailureAsync`'s switch in the UI service (§8) |
| Permission pair | `[RequirePermission(8, 9)]` on `EmploymentVerification.razor` | `ApplicationList.cs`, `SubMenuList.cs`, backend application/submenu seed data. **UI-only** — no API policy exists |
| Migration namespace | `EmploymentVerification.Data.Migrations` | Files live in `APIs/Migrations/EmploymentVerification/`; `MigrationsAssembly("APIs")` appears in both `AddEmploymentVerificationInfrastructure` and `EmploymentVerificationDbContextFactory` |
| Assembly marker | `EmploymentVerificationMarker` (namespace `BackendAPI.Modules.EmploymentVerification`) | `ServiceConfiguration.cs:13` `typeof(EmploymentVerificationMarker).Assembly`, used for Carter, MediatR and validators |
| Config keys | `EmailVerification:EmploymentVerificationUrl`, `:TokenExpiryInHours` | Env placeholders in all three `appsettings.*.json` (lines 80-83). `GetValue<int>` with a default silently swallows an unexpanded `${…}` placeholder and falls back to 72 |
| ATS contract | `IATSVerificationDataProvider` in `ATS/Shared/Contracts/` | Registered at `ATSServiceConfiguration.cs:113`; consumed via `<ProjectReference Include="..\ATS\ATS.csproj" />` |

Composition root, in the order `ServiceConfiguration.cs` runs it:
`AddEmploymentVerificationInfrastructure(configuration)` (279) → `_employmentVerificationAssembly` added
to the Carter catalog (297) → `AddEmploymentVerificationMediaTR(_employmentVerificationAssembly)` (319)
→ `AddEmploymentVerificationServices()` (337). Startup migration runs from
`APIs/Data/Extensions/DatabaseExtensions.cs:12-13` via
`EmploymentVerificationDatabaseExtensions.EmploymentVerificationInitializeDatabaseAsync(app)`, which
creates a scope, resolves the context and calls `Database.MigrateAsync()`. The module's MediatR
registration follows the guide's mandated shape — assembly scan plus `ValidationBehavior<,>` plus
`LoggingBehavior<,>`, plus `AddValidatorsFromAssembly` and `AddExceptionHandler<CustomExceptionHandler>()`.
`Modules/EmploymentVerification/GlobalUsing.cs` is what lets the slices omit `using` lines; note it pulls
in `ATS.Shared.Contracts` and `System.Security.Cryptography`, the latter for the inline
`RandomNumberGenerator` call in §2.1.

---

## Change X, also check Y

| If you change… | …also check |
|---|---|
| `HashService.Hash` (algorithm or encoding) | All three `TokenLength = 86` validators; `VerificationTokenHash`'s `HasMaxLength(128)`; every token already stored becomes unreachable |
| `CreateAndSendAsync` | The email's hardcoded "72 hours" vs `_tokenExpiryHours`; the `Pending`→`Sent` transition; the entity returned to `CreateRequestEndpoint` (S3) |
| `MarkRespondedAsync`'s `Pending \|\| Sent` predicate | The single-use guarantee (§2.3) — it is the *only* thing enforcing it. Also `ListBlockedAtsSubjectIdsAsync`, which reads the same statuses with different intent |
| `VerificationRequestStatus` members | `.HasConversion<string>()` means stored strings change — needs a data migration. Plus `ListBlockedAtsSubjectIdsAsync`, `VerifyAsync`, `GetPreviewByTokenAsync`, the UI's `GetDisplayStatus`, and `ResponseRate`'s `"Verified" or "Rejected"` literal |
| `ListBlockedAtsSubjectIdsAsync` | The availability table in the high-level doc; the *Needs request* list; the deliberate non-caching in `EmploymentVerificationCacheRepository` |
| `EmploymentVerificationCacheRepository` | **Keep `FindByTokenHashAsync` a pass-through** — caching it lets a spent token return a `Valid` preview for the TTL and breaks single use (§7) |
| `EmploymentVerificationRequest` properties | `EmploymentVerificationRequestConfiguration`; a new migration under `APIs/Migrations/EmploymentVerification/`; both `FromEntity` projections; both UI transport classes in `EmploymentVerificationDTOs.cs` |
| `SentVerificationRequestDTO` / `EmploymentVerificationPreviewDTO` | The mirror classes in `UI/FrontendWebassembly/DTO/EmploymentVerification/EmploymentVerificationDTOs.cs` — plain settable classes matched by name only. Never add `VerificationTokenHash` to either |
| Any `Results.Problem(title: …)` in the anonymous endpoints | `ReadFailureAsync`'s switch and the four render states in `VerifyEmployment.razor`/`.razor.cs` (§8) |
| `EmploymentVerificationPaths` | `PathSet` vs `PathPattern` per route (§2.4); add `Metadata["RateLimitPolicy"]` if you touch the anonymous three (S4); the UI service's relative URL strings |
| `IATSVerificationDataProvider` / its implementation | `GetAvailableATSRecordsAsync`; the `SubjectId`/`HrEmail` mappings (S9); the `.Distinct()` currently masking the `UserDetails` fan-out; whether scope resolution should finally apply (S10) |
| `ATSEmailService.SendEmailAsync` | The keyed `"ats"` registration in **two** ServiceConfig files; `CreateAndSendAsync`'s `false` → `InvalidOperationException` path and the orphaned `Pending` row it leaves (S11) |
| `[RequirePermission(8, 9)]` or the app/submenu ids | `ApplicationList.cs`, `SubMenuList.cs`, backend seed data — and remember it is UI-only; the API needs a policy added separately (S2) |
| `SecurePageBase.OnInitializedAsync` | `EmploymentVerification.razor.cs` must keep calling `base` and returning early on `!IsPageAuthorized`, or the permission attribute goes silently inert |
| Anything in this module | There are no tests to catch you (§9). Verify by hand: create a request, open the emailed link, answer it, reopen the same link and confirm 409 |
