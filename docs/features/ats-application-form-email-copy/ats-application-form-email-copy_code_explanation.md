# ATS Application Form Email Copy — code explanation

The call chain behind `ats-application-form-email-copy.md`, file by file. Read that one first for
what changed and why; this one is for the next person who has to modify it.

The change is a source for the team part of the copy list, a helper that appends the requestor to it,
and the `RequestorId` threaded down to that helper. Everything else below already existed and is
documented because the copy list now depends on it.

---

## 1. The team addresses — `ats."EmailProcessDetails"`, via `GetCopyListAsync`

This started life as a constant, `BackendAPI/Modules/ATS/Constants/ApplicationFormEmail.cs`, holding
a two-entry `CopyTeams` and nothing else. **That file is deleted.** The addresses are now rows in
`ats."EmailProcessDetails"`, read on the send path by
`Services/Settings/EmailProcessManagement/EmailProcessManagementService.cs`:

```csharp
Task<IReadOnlyList<string>> GetCopyListAsync(string emailProcess, CancellationToken cancellationToken);
```

`docs/features/ats-email-process/ats-email-process_code_explanation.md` documents that method in
full. Four properties of it matter to the code below:

- **It never throws and never returns null.** A missing row, an inactive row and an unreachable
  database all produce an empty list, and the notice still goes to the candidate. That is the same
  stance §5 takes about losing the copy on the bool path — this just adds three more ways to reach it.
- **Two processes, not one.** `AtsEmailProcess.ApplicationForm` for the first invitation,
  `AtsEmailProcess.FollowUp` for the reminder. The constant could not tell them apart; the rows can,
  and §2 now picks between them.
- **The returned list is fresh per call.** The old warning against mutating the static `CopyTeams`
  no longer applies, but §2a still copies into a new `List<string>` before appending the requestor —
  the contract returns `IReadOnlyList<string>` and the implementation is free to hand back a cached
  array.
- **It lives on `IEmailProcessManagementService`, the console's settings service.** That is the
  service the Add and Edit handlers use, and its write methods throw on bad input. This service
  holds that interface only for `GetCopyListAsync` and must never call a write: a send path has no
  operator to show a `BadRequestException` to. The field comment on the dependency says so.

The two subjects were never in the deleted constant and did not move: `InvitationSubject` and
`ReminderSubject` are `private const` at lines 374–375 of the service, next to
`SendApplicationFormToUserEmailWithResultAsync`, the one place that picks between them. The three
sibling constants (`WithdrawnEmail`, `DisputeEmail`, `SubmittedFormEmail`) kept their `Subject` for
the same reason and lost only their copy lists.

**Related pre-existing wart, not fixed by this change:** the two application form bodies do *not*
interpolate those consts. `SendAppplicationFormNotification` (~line 446) and
`BuildApplicationFormReminderNotification` (~line 505) each hardcode their `<h1>` as a duplicate
string literal. The four strings agree today; nothing enforces it. Editing a subject means editing its
header by hand. Unifying them is a change to the bodies and was out of scope for a copy list — it is
recorded in §8.

---

## 2. `Services/EndorsementSubmission/EndorsementSubmissionService.cs` — the only behaviour change

`SendApplicationFormToUserEmailWithResultAsync` (~line 377) is the single send site for both bodies.
It:

1. resolves the client name (`ResolveClientNameAsync`, returns early on a null `clientId`),
2. casts `_emailService as IAtsEmailSender` into `resultAwareSender` — a guarded cast that already
   existed for the reminder body,
3. builds the body: the reminder via `resultAwareSender.BuildApplicationFormReminderNotification`
   when `isFollowUp` **and** the cast succeeded, otherwise
   `_emailService.SendAppplicationFormNotification` (the first-invitation body),
4. picks `subject` from the two `private const` fields at lines 374–375,
5. sends.

Step 5 is the edit:

```csharp
var cc = await BuildCopyListAsync(
	isFollowUp ? AtsEmailProcess.FollowUp : AtsEmailProcess.ApplicationForm,
	requestorId,
	cancellationToken);

return await resultAwareSender.SendATSEmailWithResultAsync(
	toEmail: gmail!,
	subject: subject,
	body: emailBody,
	cancellationToken: cancellationToken,
	cc: cc);
```

The list is passed unconditionally — both messages carry one. What `isFollowUp` selects is *which
row* it comes from, reusing the same flag step 3 uses to pick the body. The teams still see the
chase as well as the original, because both rows were seeded with the same addresses; the difference
is that an operator can now change one without the other.

The `else` branch below it is unchanged and still calls `_emailService.SendATSEmailAsync(toEmail,
subject, body)`. That is the `IEmailService` bool contract in BuildingBlocks, which **has no `cc`
parameter**. On that path the candidate still gets their link and the copy is silently lost. That is
deliberate — see §5.

---

## 2a. `BuildCopyListAsync` — the requestor

```csharp
private async Task<IReadOnlyCollection<string>> BuildCopyListAsync(
	string emailProcess,
	Guid? requestorId,
	CancellationToken cancellationToken)
{
	var cc = new List<string>(
		await _emailProcessManagementService.GetCopyListAsync(emailProcess, cancellationToken));

	if (!requestorId.HasValue)
		return cc;

	var requestor = await SideEffectGuard.RunAsync(
		() => _authQueries.GetATSAssignedUserAsync(requestorId.Value, cancellationToken),
		_logger,
		$"resolve requestor {requestorId} for the application form copy list (...)",
		fallback: null,
		cancellationToken);

	if (!string.IsNullOrWhiteSpace(requestor?.UserEmail))
		cc.Add(requestor.UserEmail);
	else
		_logger.LogWarning(...);

	return cc;
}
```

Four things here are load-bearing.

**The id, not the name.** The method already receives `string? requestor` — but that is a *display
name*, a snapshot of `ICurrentUser.FullName` taken when the order was raised, interpolated into the
body's opening sentence. It is not an address and there is no way to turn it into one. `RequestorId`
is the durable handle. Both now travel together, which is why the interface has two parameters that
look redundant; the XML doc on `IEndorsementSubmissionService` says so explicitly so nobody
"simplifies" one away.

**`IAuthQueries` is the only source of a mailbox.** This is the same
`GetATSAssignedUserAsync(Guid, CancellationToken)` that `SubmittedFormEmailNotification` and
`WithdrawnEmailNotification` use to find who to *address* their notice to; here it finds who to copy.
It is cached behind `AuthCacheRepository.UserDirectory.Cache.cs`, so the per-send cost is a cache
read in the common case. It sits second-to-last in the constructor of `EndorsementSubmissionService`,
with `IEmailProcessManagementService` added after it — two unit test files construct that service by
hand and need updating whenever the list changes. The Test project carries no global usings, so both
of them also need `using ATS.Services.Settings.EmailProcessManagement;` written out.

**The team read costs nothing extra.** `GetCopyListAsync` reads the whole `EmailProcessDetails`
table, which the cache decorator holds under one key, so resolving a copy list is a cache read too.
Two cache reads per send, no queries.

**`SideEffectGuard`, not a bare await.** The single-order path in §3 runs this send *inside*
`TransactionRunner.RunAsync`. An unguarded throw from a directory read would roll back the entire
order — the invitation row, the history entry, everything — over a cosmetic copy. The guard logs and
returns the `fallback: null`, and the send carries on with the teams alone. Same reasoning as
`ResolveClientNameAsync` immediately below it.

**Appended, not prepended, and not deduplicated.** The teams are on every one of these emails and the
requestor varies, so a team mailbox threading by `Cc` sees a stable prefix. Order is otherwise
irrelevant to delivery. There is no dedup pass: a requestor whose address is also a team mailbox
would be listed twice and charged twice to the daily cap. That was unreachable while the teams were
a compiled literal — shared mailboxes, requestors are individual logins. It is reachable now: the row
is operator-editable, and nothing stops somebody adding their own login to it. `EmailCopyList.Validate`
rejects an address repeated *within* a row and cannot see the requestor appended here.

---

## 3. The four entry points above it

All of them reach the method in §2. There is no second place to add a copy list.

| Entry | File | Path |
|---|---|---|
| Single manual order | `EndorsementSubmissionService.InsertEmailInvitationRequestAsync` (~line 201) | sends **inline, inside `TransactionRunner.RunAsync`**, via the bool wrapper `SendApplicationFormToUserEmailAsync` |
| Bulk upload | `BulkEmailNotificationProcessorService.SendEmailAsync` (~line 434) | the queued, paced path |
| Resend (single and bulk) | `EndorsementSubmissionService.ResendApplicationFormAsync` / `ResendApplicationFormsAsync` | **does not send** — `RequeueEmailInvitationAsync` puts the row back to Pending with a fresh token, and the queue above delivers it |
| Follow-up chaser | `FollowUpEmailBackgroundJob` | releases rows by clearing `EmailSentAt`; the same processor sends them |

The processor decides which body applies at line ~432:

```csharp
var isFollowUp = request.LastFollowUpSentDate is not null && request.EmailSentAt is null;
```

That same flag reaches §2 as the `isFollowUp` parameter and now selects which row the copy list is
read from — so a reminder released by this job copies the `FollowUp` list, not the invitation's.

**Both sending entries retry, and §2 does not.** They do it by different mechanisms. The bool
wrapper `SendApplicationFormToUserEmailAsync` calls `SingleEmailSendRetry.SendAsync` — three
attempts on one message, 2s then 4s apart, transient faults only. The queue's `SendWithRetryAsync`
runs its own `for` loop on the same cadence, deliberately not the shared helper, because it re-reads
the pass's throttle stand-down before every attempt and its budget releases the row for a later tick
rather than ending the road. The shared method in §2 is deliberately left bare on both routes: it is
the *inner* call, so a retry there would nest inside the entry's and multiply the budget. See
[`ats-email-send-retry`](../ats-email-send-retry/ats-email-send-retry.md). The consequence for this
feature is that the copy list is built once per attempt of §2 — that is, once per attempt of the
retry — so a transient retry re-resolves the requestor. `SideEffectGuard` already makes that lookup
non-fatal, so the worst case is a repeated directory read, not a repeated failure.

**Where `RequestorId` comes from on each route.** It was already carried everywhere it was needed;
only the two send signatures had to widen to accept it.

| Entry | Source |
|---|---|
| Single manual order | `emailInvitationRequest.RequestorId`, set from `ICurrentUser` when the row is built, read inside the same `TransactionRunner.RunAsync` block that sends |
| Bulk / resend / follow-up | `request.RequestorId` off the `EmailInvitationRequest` row the processor is working — so a reminder copies the person who **raised** the order, not whoever triggered the release |

That second row is the reason the id is read off the row rather than off the ambient user: the
follow-up job and the bulk worker have no user at all.

Note the resend row: it is easy to assume resend sends inline, because it once did. It does not any
more, and the comment at `ResendApplicationFormAsync` ~line 571 explains why (a per-message SMTP
login on the request thread, bypassing the pool and the limiter, was what got a sender throttled).

`AtsAssistantService` also calls `InsertEmailInvitationRequestAsync`, so an AI-raised order copies the
teams too — for free, via the first row of the table.

---

## 4. `Services/EmailService/ATSEmailService.cs` — where `cc` becomes a header and a number

Unchanged by this work, but it is what the copy list is spent on. Four hops:

**`SendATSEmailWithResultAsync`** (~line 60) — the failover switcher. Loops registered sender
accounts, passing `cc` straight through. Each iteration asks the registry for the best account that
has not already refused *this* message. An exhausted rotation returns `Throttled`, whatever the last
account actually said, because the caller's correct response to "every account is full" is to defer
the row rather than retire it.

**`SendThroughAccountAsync`** (~line 134) — two things matter here:

```csharp
var copied = NormalizeRecipients(cc);
...
await _poolRegistry.ReportSuccessAsync(accountId, 1 + copied.Count, cancellationToken);
```

`NormalizeRecipients` runs **before** the send so the count charged to the cap is provably the same
list that goes on the wire. It drops blank entries and trims. `1 + copied.Count` is the TO address
plus everyone copied — recipients, not messages.

**`SendOverContextAsync`** → **`BuildMessage`** (~line 386):

```csharp
foreach (var copied in cc)
{
	message.Cc.Add(MimeKit.MailboxAddress.Parse(copied));
}
```

`MailboxAddress.Parse` throws on an unparseable address. `NormalizeRecipients` only removes blanks —
it does not validate, and neither does the resolver, deliberately. A malformed entry in the
`ApplicationForm` row therefore fails **every** application form send, not just one row.

That hazard used to need a code review to reach production; now it needs an operator saving a typo.
`EmailCopyList.Validate` is what stands in the way — it runs on the Add and Edit commands and
rejects an address `BulkSubjectRowValidator.IsValidEmail` will not accept. A row edited directly in
the database bypasses it entirely.

The same hazard applies to the requestor, but scoped to one order: a directory row holding a
malformed `UserEmail` fails that candidate's send. §2a's `IsNullOrWhiteSpace` check catches the blank
case; it does not validate syntax, because neither does anything else on this path.

---

## 5. Why the copy cannot live on `IEmailService`

`IEmailService` is the BuildingBlocks contract. Auth implements it (`SharedServices/Implementations/
EmailService.cs`), and so do the test fakes. `SendATSEmailAsync` there returns `bool` and takes no
`cc`.

`IAtsEmailSender` is ATS-only. It adds the result-aware overloads, `cc`, and the reminder body.

Widening the BuildingBlocks contract would force Auth and every fake to reason about a copy list they
have no teams for, to satisfy two ATS emails. So the copy rides the ATS-only path, and the cast in §2
decides which one is available. In production the keyed `"ats"` registration in
`ATSServiceConfiguration` is always `ATSEmailService`, so the fallback is unreachable there — it
exists so a future re-registration degrades instead of throwing.

---

## 6. Wiring that is not visible from any one file

| Fact | Where it bites |
|---|---|
| `AtsEmailSendLog.RecipientCount` is **summed**, not counted (`AtsEmailAccountRepository` lines ~85, ~243) | Three copies quadruple what a batch consumes: a 500-row upload charges up to 2,000. The email accounts screen reads the same sum. |
| The resend endpoints requeue rather than send | Changing the copy list changes resends too, but not until the background job picks the row up. |
| `BuildMessage` parses each address with no validation upstream | A typo in the `ApplicationForm` row breaks every send, not one. The command validator is the only guard, and a direct database edit skips it. |
| The team copy list is read at send time from a table | A row switched off, deleted or unreadable copies nobody, and the send still succeeds. Three new silent-degradation routes the constant did not have. |
| The reminder body and the copy list both depend on the same guarded cast | If the cast ever starts failing, you lose the reminder wording *and* the copy together, silently — the candidate still receives a working invitation, so nothing alerts. |
| The requestor's copy is best-effort and fails **quietly** by design | A directory outage drops every requestor from every copy list and logs a warning per send; nothing fails and no test catches it in production. The warning text is the only signal. |
| Both bodies' `<h1>` duplicate the subject consts as literals instead of interpolating them | The subject and the header can drift apart with nothing failing. The sibling notices cannot — theirs interpolate. |
| The bodies' closing sentence names `ccteam@cibi.com.ph` **and** `clientsupport@cibi.com.ph`, but the copied teams are `clientsupport` and `pre-workteam` | A deliberate mismatch, the same kind `SubmittedFormEmail` documents: `ccteam` is in the text and not on the message, `pre-workteam` is on the message and not in the text. Check both sides before changing either. |
| The agreed addresses now live in the **seed**, which runs in Production | A tester mailbox committed to `ATSInitialData.GetEmailProcesses()` reaches real candidate mail. `EmailProcessSeedTests` fails on any domain but `@cibi.com.ph`. Test against a real mailbox by editing the row in that environment, not the seed. |

---

## 7. Tests

`Test/Test/BackendAPI/Modules/ATS.UnitTests/ApplicationFormEmailCopyTests.cs` — 11 tests, all passing:

| Test | Pins |
|---|---|
| `...ShouldAddressTheCandidateAndCopyBothTeamsAndTheRequestor` | candidate is TO; teams then requestor are Cc, in that order; invitation subject and body |
| `...ShouldResolveTheRequestorMailboxFromTheDirectory` | the directory is consulted once with the id, and the *address* is copied — never the display name |
| `...ShouldStillCopyTheTeams_WhenTheOrderHasNoRequestorId` | a null id skips the lookup entirely and still copies the teams |
| `...ShouldStillSend_WhenTheRequestorNoLongerResolves` | directory returns null → teams only, send succeeds |
| `...ShouldLeaveTheRequestorOff_WhenTheirMailboxIsBlank` (×3: null, empty, whitespace) | blank addresses never reach `MailboxAddress.Parse` |
| `...ShouldStillSend_WhenTheDirectoryLookupThrows` | `SideEffectGuard` swallows it; the candidate's link still goes out |
| `...ShouldCopyTheFollowUpTeamsOnTheReminder_NotTheInvitationOnes` | `isFollowUp: true` reads the `FollowUp` row, reminder subject and body |
| `...ShouldNeverCopyTheCandidateTwice` | the candidate's address never appears in the copy list |
| `...ShouldStillSendToTheCandidate_WhenTheSenderCannotCarryACopyList` | the `IEmailService` fallback still delivers — losing the copy must never lose the send |

The four degradation tests are the point of the file, not padding: each is a route by which the
requestor drops off, and every one of them must still deliver to the candidate.

Three structural details that will bite anyone extending the file:

**The requestor id is an explicit parameter on the `SendAsync` helper, not an optional one.** There
are two overloads: `SendAsync(service, isFollowUp)` uses the standard id, and
`SendAsync(service, requestorId, isFollowUp)` takes it explicitly. A single optional `Guid? requestorId
= null` coalesced to the standard id would make "no requestor" unreachable — the null case could not
be written.

**One mock, both interfaces.** The service decides what it can send by casting the *same* object
(`_emailService as IAtsEmailSender`). Two separate mocks leave that cast returning null and silently
exercise the fallback in every test. So the ATS contract is added to the existing mock with
`_emailService.As<IAtsEmailSender>()`.

**The service is built per test, not in the constructor.** Moq requires `Mock.As<T>()` to run before
anything touches `.Object` — and the service constructor touches it. A field initialised up front
makes every result-aware test throw *"Mock type has already been initialized by accessing its Object
property."* Hence `CreateService()`, called **after** `SetupResultAwareSender()`.

**The two stubbed copy lists are deliberately different.**

```csharp
private readonly Mock<IEmailProcessManagementService> _emailProcessManagementService =
	EmailCopyListFixture
		.Returning(AtsEmailProcess.ApplicationForm, InvitationTeams)
		.AlsoReturning(AtsEmailProcess.FollowUp, ReminderTeams);
```

The fixture stubs `GetCopyListAsync` and nothing else on that interface. A test that reached a write
method would get Moq's default — which is the right outcome, because a send path calling one is the
bug, not the stub.

`InvitationTeams` and `ReminderTeams` are four fictional `@example.test` addresses, not the real
CIBI ones. Two things follow. A send path reading the wrong row fails — which is the only way the
invitation/reminder split is testable at all, since the seed gives both rows the same addresses.
And the assertions say nothing about *which* addresses CIBI actually copies: that is
`EmailProcessSeedTests`' job now, pinned where the addresses live.

That is a change of convention from the original file, which asserted against
`ApplicationFormEmail.CopyTeams`, and from the three sibling notice suites, which pinned the real
literals so an unagreed change to the copy failed a test. Splitting the two concerns — *what a
notice does with a list* here, *what the list contains* in the seed suite — is what the move to a
table forces, and it is a better split: the notice behaviour holds whatever the addresses are.

The tests stop at `IAtsEmailSender`. That the copied addresses reach the wire and are charged to the
cap is `ATSEmailService`'s business, covered by `AtsEmailFailoverTests`. A successful SMTP send cannot
be faked without a server, so the items in *Manual verification* in the feature doc still need a real
pass.

### Other test files this change touched

Neither is about the copy list; both construct or mock something whose signature moved.

| File | Why |
|---|---|
| `WithdrawnApplicationFilteringTests` | builds `EndorsementSubmissionService` by hand — needed `Mock.Of<IAuthQueries>()`, and later `Mock.Of<IEmailProcessManagementService>()`, as new constructor arguments |
| `BulkEmailNotificationProcessorServiceTests` | mocks `IEndorsementSubmissionService` — every `Setup`/`Verify` of the send needed an `It.IsAny<Guid?>()` for the new parameter |

`Fixture/EmailCopyListFixture.cs` is shared with the three sibling notice suites, which stub
`GetCopyListAsync` the same way. Its catch-all returns an empty list for any process it was not told
about, so a send path reading the wrong row copies nobody and the assertion fails.

### Suite state at the time of writing

- `ApplicationFormEmailCopyTests` — 11 passed
- `ATS.UnitTests` — 590 passed, 0 failed. The 8 failures recorded here previously were the
  tester-mailbox swap in the email constants; deleting those constants removed the cause.
- `ATS.IntegrationTests` — 334 passed, 0 failed

---

## 8. Change X, also check Y

| If you change | Check |
|---|---|
| The `ApplicationForm` or `FollowUp` row's `CCEmail` | account provisioning — each entry multiplies daily-cap consumption per send; and that every address parses, or every send fails. No rebuild is involved, so nothing forces a review |
| `BuildCopyListAsync` | the daily-cap figures in the feature doc, which assume teams + one requestor; and whether a new entry can ever collide with a team mailbox, since nothing deduplicates |
| `EmailProcessManagementService.GetCopyListAsync` | all five notices read it — see `ats-email-process_code_explanation.md`. It shares a file with the console's write methods, which throw; this one must not |
| The signature of either `SendApplicationFormToUserEmail...` overload | `BulkEmailNotificationProcessorService`, plus the mock setups in `BulkEmailNotificationProcessorServiceTests` — a `Guid?` and an `int?` next to each other make a silently-wrong positional call easy |
| The constructor of `EndorsementSubmissionService` | `WithdrawnApplicationFilteringTests` and `ApplicationFormEmailCopyTests` both build it by hand |
| `InvitationSubject` / `ReminderSubject` | the matching `<h1>` in `ATSEmailService`, which duplicates the literal rather than reading the const; and `ApplicationFormEmailCopyTests`, which pins both subjects as literals |
| The invitation or reminder **body** | the closing sentence names `ccteam@cibi.com.ph` and `clientsupport@cibi.com.ph`; a visible Cc has to keep agreeing with it, and nothing checks |
| The guarded cast in §2 | you would lose the reminder wording and the copy together; the fallback test is the only thing holding the send |
| `NormalizeRecipients` or `1 + copied.Count` | the daily-cap arithmetic for **every** ATS email, not just this one |
| `ATSInitialData.GetEmailProcesses()` | it seeds **Production**; `EmailProcessSeedTests` pins the agreed addresses and rejects any non-CIBI domain |
