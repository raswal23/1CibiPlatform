# Employment verification decline reason

**Scope:** the Employment Verification decline path — what an HR contact is asked when they
say the details are wrong, what the tracking grid then shows, and the theme of the email
that got them there. Touches the anonymous reject slice, the service, the repository write,
the tracking grid and the confirmation page. **No migration, no new route, no enum change.**

Companion: [`employment-verification-decline-reason_code_explanation.md`](employment-verification-decline-reason_code_explanation.md)
walks the call chain file by file.

## What it does

An HR contact who opens the emailed link and chooses **This is not accurate** is now asked
why before anything is recorded. Their answer is stored on the request and shown beside the
outcome in **Employment Verification → Tracking**.

| | Before | After |
|---|---|---|
| Decline action | one click, recorded immediately | opens a reason panel; Submit is disabled until something is typed |
| Stored reason | nothing — `ResponseNotes` was a dead column | the contact's text, trimmed, capped at 1000 characters |
| Tracking status chip | `Rejected` | `Verified with inaccuracy` |
| Tracking columns | 6 | 7 — **Reason for inaccuracy** added last |
| Verification email | magenta/pink gradient | the module's teal |

## Why it is shaped this way

**The relabel is display only; the stored status is still `Rejected`.** `Rejected` is
load-bearing in three places that read the enum, not a label: the availability rule that
blocks a declined segment permanently, the single-use guard on `MarkRespondedAsync`, and
`GetResponseRate`. Renaming the enum value would have meant touching all three plus a data
migration for every existing row, to change a word on a screen.

The module already had the precedent. A sent request whose link has lapsed is displayed as
`Expired` while the database still says `Sent` — see
[`employment-verification-request-tracking`](../transaction-runner/employment-verification-request-tracking/employment-verification-request-tracking.md).
"Verified with inaccuracy" is the same trick on the other branch, and it lives in the same
one function, `EmploymentVerificationDisplay.GetDisplayStatus`.

The old label was also simply wrong in a way that matters: **Rejected** reads as though CIBI
turned the request down. What actually happened is that the employer answered, and said the
details are inaccurate. The new label says who acted and what they said.

**`ResponseNotes`, not a new column.** The entity already carried `ResponseNotes` — nullable
`text`, migrated in the initial EV migration, and documented as dead: *"no writer, no
reader, anywhere."* Reusing it costs no migration and retires a column the code
explanation already flagged. The name is also the better one: it is what the responder told
us, which is exactly what a decline reason is, and it leaves room for a note on a
*confirmation* later without another schema change. `Reason` or `InaccuracyReason` would
have been narrower and needed a migration to get there.

**Required, and required at the API.** The link is single use. A contact who declines
without explaining cannot go back and add the reason — the second click reports
`TokenAlreadyUsed`. So the reason has to be captured on the one request that records the
decline, which makes it mandatory rather than nice to have. The rule lives in
`RejectRequestCommandValidator` so it is enforced for every caller, and the page checks it
too so the contact sees the message next to the box they left blank instead of a 400.

The 1000-character cap is on the same validator. This is an **anonymous** endpoint writing
free text into a column the console renders in a table cell, so the cap is a storage and
layout bound, not a guess at how much an HR contact needs.

Worth stating plainly, because it looks like a gap: this route carries **no rate-limit
policy**. None of the EV routes do — the tracking review records that as C8. It was judged
acceptable here rather than fixed in passing, because the token is both the credential and
the bound. It is an 86-character SHA-512 hash that cannot be guessed, and `MarkRespondedAsync`
only updates a row still `Pending` or `Sent`, so each token admits exactly one write and a
second attempt returns `TokenAlreadyUsed`. Abuse therefore requires holding a live token and
spends it on the first call. Adding a policy to all ten EV routes is C8's job, not this
feature's.

**An inline panel, not a dialog.** `VerifyEmployment.razor` runs on `GenericLayout`, is
reached by an external recipient with no platform account, and is built entirely from
hand-rolled `ev-confirm-*` markup — it injects no MudBlazor service. A `MudDialog` would
have needed `IDialogService` plus its providers wired into a deliberately minimal public
layout. The panel reuses the existing `.ev-confirm-actions` button row and the
`.ev-confirm-approve` / `.ev-confirm-reject` colours, so the only new CSS is the box, the
textarea and its counter.

**The reason column goes last.** It holds up to 1000 characters of anonymous prose. Placed
after the fixed-width date and status columns it cannot squeeze them, and it is clamped to
three lines with the full text on the cell's `title` so one long answer does not set the row
height for the page.

**The email theme was the last magenta left.** The module was rethemed to teal and the
private `--pink` / `--pink-dark` aliases were deleted — but only from CSS. The email body is
a C# raw string literal of inline styles, which no token sweep reaches, so it kept sending
the old magenta gradient. Its hex values now mirror the `--c-ev-*` tokens one for one and
carry a comment saying so.

They are hardcoded rather than read from `theme.css` because email clients resolve no CSS
custom properties and Outlook resolves no stylesheet at all. The consequence is real and is
the thing to remember: **a retheme in `theme.css` does not reach this email.** The comment
above the literal lists which token each value mirrors so the next person can replay it.

The email's closing line also changed, from "choose the rejection option" to "tell us what
is inaccurate" — the page no longer offers a bare rejection, and the email should not
promise one.

## Known behaviour change

Tracking's search matches on the **displayed** status, which is documented behaviour and is
how searching `expired` finds rows stored as `Sent`. So searching `rejected` now returns
nothing and `inaccuracy` returns those rows. That is consistent rather than a regression,
but it is a change an operator may notice. The reason text itself is **not** searched; if
that is wanted, add `ResponseNotes` to `Tracking.FilteredRequests`.

## How to verify

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~EmploymentVerification"
dotnet test Test/Test/Test.csproj
dotnet build 1CibiPlatform.sln
```

Manual, end to end: open a live verification link from an email, click **This is not
accurate**, and confirm the reason panel opens with Submit disabled. Click Submit while it
is empty and confirm the inline message appears rather than a request being sent. Type a
reason, watch the counter, click **Back** and confirm the panel closes and the text is
discarded. Reopen, type, submit, and confirm the success state. Then in **Tracking**
confirm the row reads `Verified with inaccuracy` with a red chip, that the reason appears in
the last column, that hovering shows the full text when it is clamped, that the stat tile
counts it, and that a `Verified` row shows an em dash for the reason.

For the email, send one request and confirm the header gradient, button and tinted table are
teal with no magenta remaining, in a client that strips stylesheets as well as one that does
not.

There is **no EV integration-test harness** — only ATS, Auth and PhilSys have a
`BaseIntegrationTest` / `IntegrationTestWebAppFactory`. Coverage here is unit level: the
validator rules and the three writers of the terminal response. Standing up a Testcontainer
harness for this module is a separate piece of work.

## What not to do

- **Do not rename `VerificationRequestStatus.Rejected`.** The availability rule blocks a
  declined segment permanently, the single-use guard treats it as terminal, and the response
  rate counts it. The label is a presentation concern owned by
  `EmploymentVerificationDisplay.GetDisplayStatus`; changing the enum changes behaviour.
- **Do not store a reason on a non-rejected row.** `MarkRespondedAsync` writes
  `ResponseNotes` unconditionally so that a confirmation clears it. Making that conditional
  would let a sentence survive beside a `Verified` outcome and read as an inaccuracy nobody
  reported. `DeclineReasonPersistenceTests` pins all three writers.
- **Do not add a second copy of the status label.** The chip, the CSS mapping and the stat
  tile all read `EmploymentVerificationDisplay.InaccurateStatusLabel`. A literal in any of
  the three drifts, and the CSS mapping fails silently — an unmatched case falls through to
  the grey `unknown` pill rather than erroring.
- **Do not pass the display label to `CountByStatus`.** It compares against the stored
  `request.Status`, so `CountByStatus("Rejected")` is correct even though the tile above it
  says "Verified with inaccuracy". The Razor comment there exists because the mismatch looks
  like a bug and is not.
- **Do not remove the reason cap or the `[FromBody]`-style body binding.** The endpoint is
  anonymous. The cap is the only bound on what an unauthenticated caller can write, and the
  body is a record of its own because the command also carries the route token.
- **Do not convert `VerifyEmployment.razor` to MudBlazor dialogs** without first wiring
  `IDialogService` and its providers into `GenericLayout`, which is deliberately minimal and
  deliberately light-only.
- **Do not expect a `theme.css` change to retheme the email.** See the comment above the
  body literal; the two are kept in step by hand.
