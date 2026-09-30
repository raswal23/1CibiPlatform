# ATS Notice Copy Lists

How ATS decides who gets copied on a withdrawal, a dispute, an application form, a follow-up or a
submitted form — and how an operator changes that list without waiting for a release.

Related: `docs/features/ats-email-accounts/ats-email-accounts.md` (which mailbox the notice is
sent *from*), `docs/features/ats-email-delivery/ats-email-delivery.md` (how one message is paced
and classified), `docs/feature-development-guide.md`.

---

## 1. What it does and why it exists

Five ATS notices copy a team alongside their real recipient. Each of those lists used to be a
compile-time array — `ApplicationFormEmail.CopyTeams` and its three siblings in
`Modules/ATS/Constants/`. Adding one address to one notice meant a code change, a review, a build
and a deployment, for a value that is really just an operational setting.

This feature moves those lists into a table, `ats."EmailProcessDetails"`, one row per notice:

| Id | EmailProcess | CCEmail | CreatedDate | IsActive |
|---|---|---|---|---|
| 1 | `Withdrawn` | `clientsupport@cibi.com.ph` | seeded | true |
| 2 | `Dispute` | `clientsupport@cibi.com.ph` | seeded | true |
| 3 | `ApplicationForm` | `clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph` | seeded | true |
| 4 | `FollowUp` | `clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph` | seeded | true |
| 5 | `SubmittedForm` | `clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph` | seeded | true |

The `EmailProcess` values are the literals in `AtsEmailProcess.All`, spelled exactly as stored —
no spaces, no hyphens. The send path matches on that string, so the spelling is data, not prose.

**Scope of this document.** The table, its seed, the three backend operations, the cutover of all
five send paths onto these rows, and the console screen that edits them. Section 5 covers how a
notice reads its list and what happens when it cannot; section 9 covers the screen.

---

## 2. The two decisions that shape everything else

### One row per notice, with the addresses in one column

The alternative — one row per *address*, with a foreign key to a process — is the more normalised
shape, and it would let the database reject a duplicate address with a unique index.

The grain here is the whole list, because that is the unit the operator works in. A copy list is
read, edited and saved as a single value: "who is copied on a dispute" is one answer, not a set
of rows to add and remove individually. A screen that edits a list wholesale, against a table that
stores it wholesale, has no partial-save state to get wrong.

**The cost is real and is paid in the validator.** The database can constrain the *column* and
not its *contents*. `a@x.com,a@x.com`, `not-an-email`, and a thousand characters of spaces are all
a perfectly valid `varchar(1000)`, and the unique index on `EmailProcess` sees the whole list as
one distinct string. Nothing downstream would catch any of it either: the send path hands each
fragment to `MimeKit.MailboxAddress.Parse`, which throws on a malformed address — and throws for
the **whole notice**, not the one bad entry. So one typo in a hand-edited list silently stops a
notice from being sent at all.

That is why `Shared/EmailCopyList.cs` exists and why the command validators are not a formality.
They are the only layer that *can* enforce what is inside the column.

### `EmailProcess` is closed to a constant — and renaming a row moves it

The send path looks a row up by this exact string, so the name is the row's identity as far as a
notice is concerned. Both writes are closed to `AtsEmailProcess.All`:

- **Add** only accepts a value in that list. A row registered against "Withdrawal" is a copy list
  nothing ever reads — and the screen would still show it as configured, which is worse than
  rejecting it. ("withdrawn" in the wrong case is a narrower case: the resolver matches
  case-insensitively, so such a row *is* read. The validator still rejects it, because the unique
  index is case-sensitive and would then allow a second row for the same notice.)
- **Edit** accepts the same list and no more, which is what makes renaming safe to offer at all.

**Renaming is a move, not a relabel.** It was not always editable: `EditEmailProcessDTO` originally
carried no `EmailProcess`, on the reasoning that retyping it would move a list from one notice to
another with nothing in the row to say it happened. The screen changed that, because an operator
who can see a row and cannot correct its name has no way to fix a mistake except by hand in the
database. What replaced the prohibition is three guards, and all three are needed:

| Guard | What it stops |
|---|---|
| The validator closes the value to `AtsEmailProcess.All`, ordinally | A rename to a name no notice sends — the failure the original prohibition existed to prevent |
| The service checks the unique index, excluding the row being edited | Two notices ending up with one name, and an ordinary save colliding with its own row |
| The dialog says out loud, the moment the selection changes, which notice stops being copied | An operator discovering the move from a team that quietly stopped receiving mail |

The `excludingId` on that index check is load-bearing rather than tidy: the name arrives in every
edit payload, so a save that changes nothing else would otherwise be compared against the row it is
holding and refused. It is only run when the name actually differs, which keeps the ordinary edit
off an uncached query and means a pair of rows differing only by case — which the validator can no
longer create, but a hand-inserted row can — does not lock an operator out of fixing either one.

A rename keeps the row's `Id` and its `CreatedDate`. It moves the list; it does not replace it.

---

## 3. The rules a copy list has to satisfy

All of these live in the validators, next to their commands, and all of them are enforced
identically on Add and Edit.

| Rule | Why it is not just tidiness |
|---|---|
| Every address passes `BulkSubjectRowValidator.IsValidEmail` | One unparseable fragment makes `MailboxAddress.Parse` throw for the whole notice |
| No address repeated, case-insensitively | The person is copied twice and charged twice against the sending account's daily cap; the unique index cannot see inside the string |
| No address over 255 characters | Would consume the column on its own |
| The **normalised** list fits in 1000 characters | Measured after spacing is dropped, because that is what gets stored |
| An empty list may be saved, but not while active | An active empty list is the one combination that fails at *send* time rather than here |

`IsValidEmail` is reused rather than restated. Its own comment says the tiers must not drift, and
a fourth definition of "valid email address" in this module would be exactly that drift.

The empty-but-inactive case is allowed on purpose: registering a notice before its addresses are
known is a reasonable thing to do. It just cannot be switched on until somebody fills it in.

### Spacing is normalised, not rejected

`clientsupport@cibi.com.ph, pre-workteam@cibi.com.ph` is accepted and stored as
`clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph`. The operator typed a list that means exactly
what they intended; rejecting it over a space would be pedantry. The length limit is applied to
the normalised form for the same reason — the spacing is not what gets saved, so it should not be
what pushes a list over the limit.

---

## 4. `IsActive` rather than deleting

There is no delete operation, and that is deliberate. A copy list is usually suspended
temporarily — a team is reorganised, an address is being migrated. The flag keeps the addresses
and the `CreatedDate` on file so restoring the list is one toggle instead of re-entering it from
memory.

An inactive row copies nobody. The notice itself is unaffected: it still goes to its real
recipient, just without the team on the Cc.

---

## 5. How a notice reads its list

One method, `EmailProcessManagementService.GetCopyListAsync`, sits between every send path and the
table. A notice asks it for a process and gets back a list of addresses:

```
WithdrawnEmailNotification     ──┐
DisputeEmailNotification       ──┤   IEmailProcessManagementService
SubmittedFormEmailNotification ──┼──►    .GetCopyListAsync         ──► IEmailProcessRepository ──► cache
EndorsementSubmissionService   ──┘        (one call per notice)          (whole table, one key)
  ApplicationForm / FollowUp
```

It is the same service the console uses to add and edit these rows — one object owning one table,
rather than a separate reader alongside it. There is no third consumer, and a lookup each send path
wrote itself would decide the failure question five different ways. The send paths depend on the
interface, so none of them can reach a write: the notifications only ever call this one method.

### It never throws, and never returns null

**Every** failure — no row for the process, the row switched off, the database not answering —
produces the same empty list, and the notice still goes out. The read is wrapped in
`SideEffectGuard`, and the send path treats an empty Cc as a safe no-op.

This is the opposite stance from the validators in section 3, and both are right. A *write* is the
moment to be strict: the operator is present, the mistake is cheap to report, and rejecting it
costs nothing. A *send* is not. The work above it has already committed — an order withdrawn, a
form submitted — and the recipient is waiting. Losing the copy degrades the notice; losing the
notice breaks the thing the operator was told had happened.

Both stances now live on one interface, which is the one thing to be careful about when editing it.
`AddEmailProcessAsync` and `EditEmailProcessAsync` throw; `GetCopyListAsync` cannot. The read also
does **not** call `GetEmailProcessesAsync` beside it — that method is the console's and is meant to
fail loudly — it goes to the repository through its own guard. Making the three "consistent" would
break one caller or the other.

The failures are still distinguished in the log, because they mean different things. A **missing
row** logs a warning: every value in `AtsEmailProcess.All` is seeded, so its absence means the seed
did not run or somebody deleted the row. An **inactive row** logs information: that is an operator's
decision, not a fault.

### It reads the whole table

Five rows, filtered in memory, rather than a query for one. The whole-table read is the one the
cache decorator holds under `emailprocess_v1_all`; a by-process query would miss that entry and put
a database round trip on every single notice.

### It does not filter malformed addresses

A typo in a hand-edited row reaches `MimeKit.MailboxAddress.Parse` and fails the send. Dropping it
instead would be quieter and worse: the team would be silently uncopied for as long as the typo
survived, and nothing would ever report it.

### The invitation and the reminder read different rows

`EndorsementSubmissionService` resolves `ApplicationForm` or `FollowUp` depending on which message
it is sending. They were seeded with the same addresses, so the cutover changed nothing — but the
reminder chases a candidate who has gone quiet and the invitation does not, and the list that wants
to hear about the second is not obviously the list that wants to hear about the first. A single
literal could not tell them apart; two rows can.

### What stayed compiled in

`WithdrawnEmail`, `DisputeEmail` and `SubmittedFormEmail` still hold their `Subject`.
`ApplicationFormEmail`, which held only a copy list, was deleted. A subject is a different kind of
value: changing one rewords the message, which is a copy decision made with the body beside it.

---

## 6. What a change costs at send time

Every address in a copy list is a real recipient to the provider, and **each one is charged
against the sending account's daily cap** alongside the actual recipient — the arithmetic
`ats-email-accounts.md` explains in full. The send log records the TO address only, so the log
undercounts what a batch really consumed.

Adding a third address to the application-form list does not add a third of a message to a batch
of 500 — it adds 500 recipients, taking that batch from 1,500 to 2,000 against
`DefaultDailySendLimit`. This is the real cost of making the list editable, and it is the reason the
screen says so: both dialogs carry a note stating that every address is charged alongside the real
recipient, and the board's intro repeats it. Nothing enforces a budget here, and nothing should —
the copy is often the point of the notice — but an operator adding an address should not have to
work out afterwards what it cost.

---

## 7. Caching

The list read is cached under one key, `emailprocess_v1_all`, tagged `CacheTags.EmailProcess`, and
both writes invalidate the tag. Two reads deliberately bypass the cache:

- the **single-row fetch for an edit**, because it returns a *tracked* entity — a cached instance
  would be a detached object shared between requests, and the second edit would save the first
  one's changes;
- the **uniqueness guard**, because an answer from cache defeats the point of asking.

The send path reads through the same cached entry, so resolving a copy list costs no query of its
own — and an edit takes effect on the next notice, because the write invalidates the tag.

---

## 8. Known gaps

- **No delete, by design.** See section 4.
- **Add is normally refused, and that is correct rather than broken.** The seeder matches on the
  process rather than on an empty table, so every environment already has a row for all five
  notices. The Add dialog's dropdown lists all five anyway, and a duplicate comes back as the
  server's own `400` in a snackbar. What Add is for is *recovery* — putting back a row the seed did
  not create or somebody removed by hand — not for extending the vocabulary, which is a code change
  to `AtsEmailProcess` and a redeploy.
- **Two notices name addresses in their body prose that nothing keeps in step with the row.** The
  dispute and submitted-form bodies close by telling the reader to contact `ccteam@cibi.com.ph` and
  `clientsupport@cibi.com.ph`. Those are literals in `ATSEmailService`, unrelated to the Cc. An
  operator retiring a mailbox from a copy list leaves the body still telling candidates to write
  to it.
- **Seed data ships to production.** `AppConfiguration.UseEnvironmentAsync` seeds Development,
  Sandbox, UAT **and** Production. A tester mailbox committed to `ATSInitialData.GetEmailProcesses`
  reaches real candidate mail. `EmailProcessSeedTests` fails on any address outside `@cibi.com.ph`
  for exactly that reason. (The constants this feature replaced spent a stretch swapped to a
  personal gmail address for manual testing; deleting them ended that class of accident, and this
  test is what stops it reappearing in the seed.)
- **Nobody except a platform super admin can see the screen until module 16 is granted.** Super
  admins are given every id in `ModuleList.List` client-side, so they get the tab on the next
  deploy with no grant. Everyone else needs one, and because 16 is in
  `RestrictedAdministrationModuleIds` beside Audit Trail, only a super admin or the ATS platform
  manager sees it in the User Management module picker to grant it. Granting it gives both email
  tabs; there is no way to give one without the other. See section 9.

---

## 9. The console screen

**Email Management → Email Receiver**, at `/s&i/ats/emailmanagement`. Email Management is a host
page with two tabs — **Email Accounts**, the mailboxes notices are sent *from*, and **Email
Receiver**, this feature: the mailboxes notices are copied *to* — in the same shape Settings uses
for Package / Client / Role.

**Both tabs are one permission: ATS module 16**, `AtsModuleIds.EmailManagement` on the backend and
`{ 16, ("emailmanagement", "Email Management", …) }` in `ShareData/ATS/ModuleList.cs` on the
frontend. The two halves of email administration are granted together or not at all; there is no way
to give an operator the sender mailboxes without also giving them the copy lists. That is a
deliberate trade — they are one screen, staffed by the same people, and a permission nobody can
split is a permission nobody forgets to grant.

Grouping them was a UI-level change. Neither screen's loading, validation, services or backend
endpoints moved, and both still carry `RequireATSModule(16)` as a second check. What did change:

- Module 16's path moved from `emailaccounts` to `emailmanagement`, and **neither tab is a route any
  more** — both lost their `@page`. A second URL per tab would need its own branch in
  `ATSLayout.CanAccessRoute` with no module behind it, which is the ungrantable-entry trap the
  Notifications comment in `ModuleList` records.
- `/s&i/ats/emailaccounts` therefore no longer resolves. The one thing that linked to it — the
  `EmailAccountsExhausted` notification raised when every sender is capped — now points at the host,
  which opens on the Accounts tab.
- There is no special case in `CanAccessRoute` and no sidebar indirection, precisely because there
  is only one id: the generic `ModuleList` lookup resolves `emailmanagement` to 16 and that is the
  whole check.

Module 16 has to be named in **two** places on the frontend, and the second is the one worth knowing
about because getting it wrong fails silently:

| Where | What it does | If 16 is missing |
|---|---|---|
| `ModuleList.List` | Drives the sidebar entry and route resolution | No sidebar link, and `/s&i/ats/emailmanagement` is refused |
| `EmailManagement.razor` → `RequireATSModule(16)` | Lets the host open, and with it both tabs | The page redirects to `/access-denied` before it renders |

Module **17 existed for one uncommitted iteration of this screen and is retired, not free.** Any
database that booted in the meantime has an orphan row the runtime seeder will not remove, because
it only inserts ids the catalogue is missing. The cleanup SQL, and why reusing 17 without running it
would silently grant a future feature to whoever held the old one, are at the end of
`ats-email-process_code_explanation.md`.

The screen itself is a table of the five rows with an Add button above it and a pencil on each row.
Three things about it are decisions rather than defaults:

**It reads the whole table and filters in the browser.** There is no cursor. One row per notice
fits on one screen, and the backend caches exactly that whole-table read under
`emailprocess_v1_all` — a paged endpoint would either miss the cache on every page or add paging
scaffolding around a list that does not need it. `TableComponent`'s `Items` parameter renders the
same card without the keyset walk, which is what the sender-account board beside it does too.

**The copy list is typed as chips, not as a comma-separated string.** The column stores one string,
and that string is the reason a malformed fragment is expensive: it fails the whole notice at send
time. A chip is checked the moment it is added — shape, duplicate, per-address length, total length
— so the operator hears about a typo while it is still one keystroke to fix. `EmailCopyListDraft`
holds those list-shaped rules and mirrors `EmailCopyList`; the shape check is delegated to the
existing `EmailValidationService` rather than given a second regex, because a fourth definition of
"valid email address" in this codebase is exactly the drift the backend's copy warns about. The
server still validates everything again. Chips are the early warning, not the authority.

**It says what an address costs.** Section 6's arithmetic is on the screen: both dialogs carry a
note that every address is charged against the sending account's daily cap alongside the real
recipient, and the board's intro repeats it. This closes the gap the earlier version of this
document recorded — that nothing told an operator adding a third address that they had just raised
a 500-order batch from 1,500 recipients to 2,000.

Renaming warns too, for the reason in section 2: the dialog names both notices the moment the
selection differs from what was loaded, so "Withdrawn order stops copying anyone and Dispute starts
copying these mailboxes instead" is read before Save is pressed rather than inferred afterwards
from a team that went quiet.
