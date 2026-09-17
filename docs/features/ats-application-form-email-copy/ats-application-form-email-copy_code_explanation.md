# ATS Application Form Email Copy — code explanation

The call chain behind `ats-application-form-email-copy.md`, file by file. Read that one first for
what changed and why; this one is for the next person who has to modify it.

The change itself is two files: a new constant, and one argument on one existing call. Everything
else below already existed and is documented because the copy list now depends on it.

---

## 1. `BackendAPI/Modules/ATS/Constants/ApplicationFormEmail.cs` — new

```csharp
public static class ApplicationFormEmail
{
	//public static readonly IReadOnlyCollection<string> CopyTeams =
	//[
	//	"ccteam@cibi.com.ph",
	//	"pre-workteam@cibi.com.ph"
	//];

	// Test recipients while the branch is being verified ...
	public static readonly IReadOnlyCollection<string> CopyTeams =
	[
		"svaldemoro@cibi.com.ph",
		"angel.condensada11@gmail.com"
	];
}
```

`IReadOnlyCollection<string>` rather than a `const string`, because this copies two mailboxes.
`SubmittedFormEmail` uses the same shape for the same reason; `WithdrawnEmail` and `DisputeEmail`
copy one team each and use a `const string`.

**It holds no `Subject`, unlike all three siblings.** Each of those is *one* notice with one subject,
interpolated into the body's header (`BuildSubmittedFormNotification` renders
`{SubmittedFormEmail.Subject}` directly into its `<h1>`), so one constant keeps the preview line and
the header in step. This is *two* bodies with two subjects, already held as `InvitationSubject` and
`ReminderSubject` at lines 374–375 of the service — next to
`SendApplicationFormToUserEmailWithResultAsync`, the one place that picks between them. Moving a pair
of subjects here would separate them from that choice and buy nothing.

**Related pre-existing wart, not fixed by this change:** the two application form bodies do *not*
interpolate those consts. `SendAppplicationFormNotification` (~line 446) and
`BuildApplicationFormReminderNotification` (~line 505) each hardcode their `<h1>` as a duplicate
string literal. The four strings agree today; nothing enforces it. Editing a subject means editing its
header by hand. Unifying them is a change to the bodies and was out of scope for a copy list — it is
recorded in §8.

The active list is the tester's mailboxes with the real block commented out above it — the same swap
already present in the three siblings on this branch. See *Before release* in the feature doc.

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

Step 5 is the edit. One argument:

```csharp
return await resultAwareSender.SendATSEmailWithResultAsync(
	toEmail: gmail!,
	subject: subject,
	body: emailBody,
	cancellationToken: cancellationToken,
	cc: ApplicationFormEmail.CopyTeams);
```

The list is passed unconditionally — it does not branch on `isFollowUp`. That is the decision
recorded in the feature doc: the teams see the chase as well as the original.

The `else` branch below it is unchanged and still calls `_emailService.SendATSEmailAsync(toEmail,
subject, body)`. That is the `IEmailService` bool contract in BuildingBlocks, which **has no `cc`
parameter**. On that path the candidate still gets their link and the copy is silently lost. That is
deliberate — see §5.

---

## 3. The four entry points above it

All of them reach the method in §2. There is no second place to add a copy list.

| Entry | File | Path |
|---|---|---|
| Single manual order | `EndorsementSubmissionService.InsertEmailInvitationRequestAsync` (~line 201) | sends **inline, inside `TransactionRunner.RunAsync`**, via the bool wrapper `SendApplicationFormToUserEmailAsync` |
| Bulk upload | `EmailNotificationProcessorService.SendEmailAsync` (~line 434) | the queued, paced path |
| Resend (single and bulk) | `EndorsementSubmissionService.ResendApplicationFormAsync` / `ResendApplicationFormsAsync` | **does not send** — `RequeueEmailInvitationAsync` puts the row back to Pending with a fresh token, and the queue above delivers it |
| Follow-up chaser | `FollowUpEmailBackgroundJob` | releases rows by clearing `EmailSentAt`; the same processor sends them |

The processor decides which body applies at line ~432:

```csharp
var isFollowUp = request.LastFollowUpSentDate is not null && request.EmailSentAt is null;
```

The copy list is indifferent to that flag. If you ever need it to branch, that boolean is already
threaded through to §2 as the `isFollowUp` parameter.

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
it does not validate. A malformed entry added to `CopyTeams` therefore fails **every** application
form send, not just one row. Treat the constant as production configuration.

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
| `AtsEmailSendLog.RecipientCount` is **summed**, not counted (`AtsEmailAccountRepository` lines ~85, ~243) | Two copies triple what a batch consumes: a 500-row upload charges 1,500. The email accounts screen reads the same sum. |
| The resend endpoints requeue rather than send | Changing the copy list changes resends too, but not until the background job picks the row up. |
| `BuildMessage` parses each address with no validation upstream | A typo in `CopyTeams` breaks every send, not one. |
| The reminder body and the copy list both depend on the same guarded cast | If the cast ever starts failing, you lose the reminder wording *and* the copy together, silently — the candidate still receives a working invitation, so nothing alerts. |
| Both bodies' `<h1>` duplicate the subject consts as literals instead of interpolating them | The subject and the header can drift apart with nothing failing. The sibling notices cannot — theirs interpolate. |
| The bodies' closing sentence names `ccteam@cibi.com.ph` **and** `clientsupport@cibi.com.ph`, but only `ccteam` and `pre-workteam` are copied | Same deliberate mismatch `SubmittedFormEmail` documents: `clientsupport` is in the text and not on the message, `pre-workteam` is on the message and not in the text. Check both sides before changing either. |
| `SubmittedFormEmail`, `WithdrawnEmail`, `DisputeEmail` and now `ApplicationFormEmail` all carry the tester swap | They must be restored in one commit. Restoring three of four leaves one notice going to the wrong mailboxes. |

---

## 7. Tests

`Test/Test/BackendAPI/Modules/ATS.UnitTests/ApplicationFormEmailCopyTests.cs` — four tests, all
passing:

| Test | Pins |
|---|---|
| `SendApplicationForm_ShouldAddressTheCandidateAndCopyBothTeams` | candidate is TO, both teams are Cc, invitation subject and body |
| `SendApplicationForm_ShouldCopyBothTeamsOnTheFollowUpReminder_Too` | same copy list with `isFollowUp: true`, reminder subject and body |
| `SendApplicationForm_ShouldNeverCopyTheCandidateTwice` | the candidate's address never appears in the copy list |
| `SendApplicationForm_ShouldStillSendToTheCandidate_WhenTheSenderCannotCarryACopyList` | the `IEmailService` fallback still delivers — losing the copy must never lose the send |

Two structural details that will bite anyone extending the file:

**One mock, both interfaces.** The service decides what it can send by casting the *same* object
(`_emailService as IAtsEmailSender`). Two separate mocks leave that cast returning null and silently
exercise the fallback in every test. So the ATS contract is added to the existing mock with
`_emailService.As<IAtsEmailSender>()`.

**The service is built per test, not in the constructor.** Moq requires `Mock.As<T>()` to run before
anything touches `.Object` — and the service constructor touches it. A field initialised up front
makes every result-aware test throw *"Mock type has already been initialized by accessing its Object
property."* Hence `CreateService()`, called **after** `SetupResultAwareSender()`.

**The cc assertion reads `ApplicationFormEmail.CopyTeams` rather than pinning literals.** This departs
from `SubmittedFormEmailNotificationTests`, which pins `ccteam@cibi.com.ph` and
`pre-workteam@cibi.com.ph` so an unagreed change to the copy fails a test. That convention is right
and is currently doing its job — the constants are swapped to a tester's mailboxes, and the three
sibling notice tests are red because of it. Pinning the same literals here would add a fourth red test
reporting an already-reported fact, and would assert nothing about what this file exists to cover:
which addresses are copied, on which of the two bodies, and what happens when the sender cannot carry
a copy list. Those hold whatever the constant contains.

The tests stop at `IAtsEmailSender`. That the copied addresses reach the wire and are charged to the
cap is `ATSEmailService`'s business, covered by `AtsEmailFailoverTests`. A successful SMTP send cannot
be faked without a server, so the items in *Manual verification* in the feature doc still need a real
pass.

### Suite state at the time of writing

- `ApplicationFormEmailCopyTests` — 4 passed
- `ATS.UnitTests` — 504 passed, 8 failed. All 8 pre-existing and unrelated: the three sibling notice
  test classes asserting the real team literals against the tester swap, which was already in the
  working tree before this change.
- `ATS.IntegrationTests` — 310 passed, 0 failed

---

## 8. Change X, also check Y

| If you change | Check |
|---|---|
| `ApplicationFormEmail.CopyTeams` | account provisioning — each entry multiplies daily-cap consumption per send; and that every address parses, or every send fails |
| The invitation or reminder **body** | the closing sentence names `ccteam@cibi.com.ph`; a visible Cc to that mailbox has to keep agreeing with it |
| `InvitationSubject` / `ReminderSubject` | the matching `<h1>` in `ATSEmailService`, which duplicates the literal rather than reading the const; and `ApplicationFormEmailCopyTests`, which pins both subjects as literals |
| The guarded cast in §2 | you would lose the reminder wording and the copy together; the fallback test is the only thing holding the send |
| `NormalizeRecipients` or `1 + copied.Count` | the daily-cap arithmetic for **every** ATS email, not just this one |
| Anything in `Constants/` carrying the tester swap | restore all four together |
