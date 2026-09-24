# Employment Verification — automatic sending

Verification requests now go out on a schedule, triggered by the candidate submitting
their application form, instead of waiting for an operator to open a screen and click
Send for each one.

## What it does and why

### Before

The manual trigger was deliberate. It proved the token → email → verify/reject path end
to end while the data feeding it was still thin. Two things have changed: the application
form now captures a supervisor email per employer, and the contact directory holds 7,357
vetted company mailboxes. The flow no longer needs a human to decide who to write to.

### Three problems the old path had

Automating on top of these would have industrialised them, so they are fixed here rather
than left as follow-ups.

**It emailed the wrong person.** `ATSVerificationDataProvider` returned
`HrEmail = user.UserEmail` — the ATS *requestor's* address. `HrName` came from
`Emp1SupervisorName`. Name and email described two different people, and every
verification request ever sent went to our own recruiter rather than to the former
employer. The fields are now `SupervisorName`/`SupervisorEmail`, named for what they
contain rather than what they were meant for; the old names are what let this sit
unnoticed.

**It only ever read the first employer.** The provider read `Emp1*` and discarded `Emp2*`
and `Emp3*`. A candidate with three previous employers produced one record and two
silently lost ones. The contract now returns one record per *segment*.

**One employer would have blocked the others.** All three employers of an order share an
`AtsSubjectId`, and the availability check de-duplicated on that column alone. Sending for
the first employer would have suppressed the other two permanently — the feature would
have looked like it worked while sending exactly one email per candidate forever.

### What a "segment" is

The application form has three employer slots. A **segment** is which slot: 1, 2 or 3.
The whole employer record travels together — company, position, dates, supervisor — so
segment 2's dates are never asked of segment 1's employer. It is positional, not
chronological; segment 1 is simply the first box on the form.

All three live in one `ats.ProfessionalExperiences` row as `Emp1*`/`Emp2*`/`Emp3*` column
groups. The contract unpivots them.

## How it works

Every five minutes the job asks for eligible segments and, for each one:

1. **Consent.** The candidate must have ticked *permission to contact* for that employer.
   Blank, absent or unrecognised reads as no — absence of consent is not consent. A
   segment without it is never sent and never gets a row.
2. **Recipient.** The supervisor address from that segment must be **listed in the contact
   directory**. If it is not, nothing is sent.
3. **Send.** `CreateAndSendAsync` is reused unchanged. It mints a token whose emailed link
   embeds the exact 86-character hash the verify endpoints validate, so reimplementing it
   would have produced links those endpoints reject.

### The directory is an allow-list, not a preference

The address is supplied by the **candidate**, on their own application form. Writing to it
unchecked would let someone nominate who verifies their own employment history — a friend's
inbox confirms whatever they like. Requiring the address to appear in the directory first
is what closes that.

Matching is on the address, exact, case-insensitive (contacts are stored lower-cased, so
the form's value is folded before comparing). Not on the company name: two firms can share
a name, and the thing being authorised is a mailbox.

**Expect a low send rate at first.** The directory holds general company mailboxes such as
`recruitment@concentrix.com`, while the form captures a named person such as
`maria.santos@concentrix.com`. Those rarely coincide, so most segments will sit unsent
until someone adds the address. That is the gate working, not a fault — the queue's
**No email** and **Pending check** rows are the worklist of addresses worth adding.

### Sent, and still waiting

**A row exists = sent. No row = still waiting.** There is no "sent" flag anywhere.

```
                    seg 1        seg 2        seg 3
T+0   submitted       ·            ·            ·      all waiting
T+5m  pass 1        [Sent]      [Sent]         ·      3 skipped, no consent
T+2d  emp 1 replies [Verified]  [Sent]         ·
T+4d  seg 2 expires [Verified]  [Sent(exp)]    ·      expiry releases the segment
T+4d  pass N        [Verified]  [exp][Sent]    ·      re-sent as a NEW row
```

Segment 2 acquiring a *second* row is why a boolean flag could not work: a re-send needs a
new token, a new expiry and its own outcome. Rows hold that.

**Segments never cross.** A sent segment 1 blocks only the pair `(subject, 1)`; segment 2
is still eligible and is sent to *segment 2's* recipient, resolved from segment 2's own
company and supervisor fields. The recipient is resolved per record inside the loop, never
carried between iterations.

Two cases worth stating because they look like bugs and are not:

- **Two segments resolving to the same mailbox** — a candidate who worked at two
  subsidiaries sharing one HR inbox — produce **two** emails to that inbox, each with its
  own token and naming its own company and dates. They are two separate employment claims
  and confirming one says nothing about the other.
- **A failed segment 1** leaves no blocking row, because the failure marks it `Rejected`,
  so the next pass retries segment 1 *to segment 1's address* alongside segment 2.

The states the queue screen shows — Pending check, No consent, No email — are **derived**,
not stored. They are what is true of a segment ATS offers that EV has no row for. Note the
queue says *Pending check* rather than *Queued*: the screen does not know whether the
address is in the directory, because checking per row would mean a lookup per row.

### Why the column is on the EV table

`ats.ProfessionalExperiences` is untouched. "Did Employment Verification send an email" is
EV's state, not ATS's; putting it in ATS's table would make that module carry a field only
EV writes and would need a contract method for EV to write back across the module
boundary — the coupling the shared-contract folder exists to prevent.

### Which statuses block, and why

| Status | Blocks? | Reasoning |
|---|---|---|
| `Pending` | yes | committed but not yet sent; blocking stops a second attempt racing the first |
| `Sent` | while the token is live | an unanswered link that lapsed is worth retrying |
| `Verified` | permanently | answered |
| `Rejected` | **permanently** | answered — "not accurate" |
| `Expired` | no | the send itself failed; retry it |

**`Rejected` blocking is a change from the manual flow.** It used to release the segment,
meaning "an operator may try this employer again" — reasonable when a human decided. With a
job running every five minutes it meant re-mailing an employer who had just declined, which
is the one outcome a verification flow must never produce. That is a real bug this feature
introduced and then fixed; it was caught by a rejected request reappearing in the queue.

### Failure

`CreateAndSendAsync` commits the row before sending. A failed send used to leave it
`Pending`, which blocks its segment permanently with no expiry and no sweeper. Rare and
visible when a human clicked Send; silent and cumulative once a job does it.

The row is now marked **`Expired`** on send failure. Not `Rejected` — that now means the
employer answered no and blocks for good, so reusing it would both disguise a delivery
failure as a decline and strand the segment permanently. `Expired` was declared in the enum
and never assigned by anything; this is what it is for.

## Configuration

| Key | Env var | Default | Purpose |
|---|---|---|---|
| `EmailVerification:AutoSendEnabled` | `EMAILVERIFICATION__AUTOSENDENABLED` | `false` | Kill switch. **Ships off.** |
| `EmailVerification:AutoSendMaxPerPass` | `EMAILVERIFICATION__AUTOSENDMAXPERPASS` | `200` | Segments per pass |
| `EmailVerification:TokenExpiryInHours` | `EMAILVERIFICATION__TOKENEXPIRYINHOURS` | `72` | Link lifetime |
| `EmailVerification:EmploymentVerificationUrl` | `EMAILVERIFICATION__EMPLOYMENTVERIFICATIONURL` | — | Base of the emailed link |

### Where the values actually come from

**The `${…}` text in `appsettings.*.json` is not expanded by anything.** No code in this
repo substitutes it. The values arrive through the default `AddEnvironmentVariables()`
provider, which is registered last and therefore overrides the JSON by key — `__` maps to
the `:` section separator, so `EMAILVERIFICATION__AUTOSENDENABLED` lands on
`EmailVerification:AutoSendEnabled`.

The placeholders are documentation of intent, not a mechanism. If the environment variable
is missing, the literal string `${EMAILVERIFICATION__AUTOSENDENABLED}` is what config
holds — which for a `GetValue<bool>` reads as the default `false`, but for the **URL**
means the emailed link is built from that literal text. That is the real hazard here, not
the boolean.

So adding a key to `appsettings` is only half the job. The other half is
**`.env` at the repository root** — gitignored, `env_file:` for both compose services, and
created by hand per the README. Add these two lines beside the existing pair:

```bash
# ---------- Employment Verification ----------
EMAILVERIFICATION__EMPLOYMENTVERIFICATIONURL=http://localhost:5134/employmentverification/verify
EMAILVERIFICATION__TOKENEXPIRYINHOURS=72
EMAILVERIFICATION__AUTOSENDENABLED=false
EMAILVERIFICATION__AUTOSENDMAXPERPASS=200
```

Because `.env` is gitignored there is no committed template, so this block is the record —
every developer and every deployed environment needs it set independently.

Off by default on purpose: enabling it is the moment real former employers start receiving
mail, which should be a decision rather than a side effect of deploying.

The section was missing entirely from `appsettings.UAT.json` and is still absent from
`appsettings.Testing.json` — without it the base URL degrades to `""` and links render as
`/{hash}`. UAT is fixed here; confirm the environment variables are set before enabling
sending anywhere.

## How to verify it

```powershell
dotnet build 1CibiPlatform.sln
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~EmploymentVerification"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~IntegrationTests"
```

End to end: set `AutoSendEnabled` true in Development, submit an application form with two
employers and permission to contact, and within five minutes expect two rows with
`EmploymentSegment` 1 and 2 and distinct tokens. Click through verify on one and confirm
the other is unaffected.

```sql
SELECT "EmploymentSegment", "PreviousEmployer", "HrEmail", "RecipientSource", "Status"
FROM employment_verification."EmploymentVerificationRequests"
WHERE "AtsSubjectId" = '<order id>'
ORDER BY "EmploymentSegment";
```

## What not to do

- **Do not block on `AtsSubjectId` alone.** It is the bug the discriminator exists for: one
  employer would silently suppress the candidate's others.
- **Do not add a sent-flag to `ats.ProfessionalExperiences`.** It is the wrong module's
  table, and a single flag cannot express a re-send.
- **Do not send to an address the directory does not list**, however plausible it looks.
  The candidate chose it; the directory is what authorises it.
- **Do not "fix" the low send rate by loosening the gate.** The fix is adding addresses to
  Contacts. Fuzzy or company-level matching would reintroduce exactly the hole the
  allow-list closes.
- **Do not treat a blank consent value as permission.** Fail closed.
- **Do not reimplement `CreateAndSendAsync`.** The emailed link must carry the exact stored
  hash, and the verify validators enforce its 86-character length.
- **Do not let the pass run uncapped.** The first run after deployment sees every
  in-progress order at once and shares a rate-limited mailbox pool with ordinary invitations.

## Related

- [Code walkthrough](employment-verification-auto-send_code_explanation.md)
- [Contact directory](../employment-verification-contact-directory/employment-verification-contact-directory.md)
- [Request tracking](../transaction-runner/employment-verification-request-tracking/employment-verification-request-tracking.md)
