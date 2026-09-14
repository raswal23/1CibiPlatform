# ATS New Order: Screening Type Gate & Candidate Identity Fields

**Date:** 2026-09-14
**Scope:** ATS New Order (single and bulk), `ats.EmailInvitationRequest` and
`ats.BulkUploadFileDetails` schema, package fetching, the email invitation worker

## What it does

The single-candidate New Order form now starts with the **screening type**, and the
order captures the candidate's identity when no application form will be sent:

1. **Section order changed** to: 1 Package, 2 Personal information, 3 Contact
   details, 4 Processing speed.
2. The Package section leads with a **Screening type** select (Manual / Data). The
   **package select is disabled until a screening type is chosen**, and then lists
   only packages of that type. An info note under the section explains the choice:
   Manual — an application form invitation is emailed to the candidate; Data — no
   form is sent and only the identity fields are required.
3. Personal information gains **Date of birth, SSS number, TIN number**. They are
   **required only for Data screening**, enforced in the MudForm and again in the
   backend command validator.
4. The order row stores four new columns: `AutoChasing`, `DateOfBirth`,
   `SSSNumber`, `TINNumber`.
5. **Bulk invite** carries the same gate: a screening type is chosen on the file,
   stored on `ats.BulkUploadFileDetails.AutoChasing`, and stamped onto every order
   the file creates. A data file's CSV must carry three extra columns.
6. **Only manual orders are emailed an application form** — the invitation worker
   claims `AutoChasing IS TRUE` rows and nothing else, the single-order path skips
   its inline send, and resend is refused for anything that is not manual.
7. A data order's **email columns are all `NULL`** — it holds no position in the
   send queue, so it has no status in it either.

## Value mapping

Same convention as package management (`docs/ats-package-screening-type.md`):

| Meaning | `AutoChasing` |
|---|---|
| Manual screening | `true` (1) |
| Data screening | `false` (0) |
| Not set / unknown | `NULL` |

A package whose `AutoChasing` is `NULL` (unclassified) appears in **neither** the
Manual nor the Data package list — "not set" must never pass for either type.

## How it works

- **Schema** — migration `20260914150000_AddCandidateIdentityToEmailInvitationRequest`
  adds nullable `AutoChasing boolean`, `DateOfBirth date`, `SSSNumber`/`TINNumber
  varchar(255)` to `ats.EmailInvitationRequest`, plus nullable `AutoChasing boolean`
  to `ats.BulkUploadFileDetails`. Hand-written (EF CLI unavailable in this
  environment) with `[DbContext]`/`[Migration]` attributes inline; the model
  snapshot was updated in the same commit. Nullable on purpose: public API orders,
  the AI assistant and all legacy rows never set them.
- **Backfill** — migration `20260914160000_BackfillLegacyAutoChasing` sets
  `AutoChasing = true` on every pre-existing order and bulk file. Data screening did
  not exist when those rows were created, so they are all manual; leaving them `NULL`
  would strand any that still need an invitation (see *The email invitation worker*).
  It also runs `ADD COLUMN IF NOT EXISTS` on `ats.BulkUploadFileDetails` — see the
  caveat below. `Down` is deliberately empty: a backfilled `true` cannot be told apart
  from one a user chose.
- **Package fetching** — `GetPackagesPageAsync`/`CountPackagesAsync` (repository,
  cache decorator, `PackageManagementService.GetPackagesAsync`, `getpackages`
  endpoint, UI service) accept an optional `autoChasing` filter. Cache keys moved to
  `package_v5_*` and include the filter. The New Order form filters client-side from
  the already-loaded list; the query parameter exists for callers that want the
  server to filter.
- **Web validator** (`EmailInvitationRequestCommandValidator`) — `AutoChasing`
  `NotNull` (the console always sends it); when `false` (Data): `DateOfBirth`
  required and in the past, `SSSNumber` exactly 10 digits, `TINNumber` 9–12 digits
  (digit rules follow `OMSTicketPayloadMapper.NormalizeGovernmentId`). These rules
  are deliberately **not** in the shared service so the public API and assistant
  paths (which send `AutoChasing = null`) keep working unchanged.
- **Service cross-check** — `OrderInputValidator.ValidateAsync` now returns the
  matched package's `AutoChasing` on `ValidatedOrderInput` (no extra query).
  `EndorsementSubmissionService.InsertEmailInvitationRequestAsync` rejects a
  non-null caller screening type that disagrees with the package
  (`BadRequestException`), then **snapshots the package's classification onto the
  order** — the stored value comes from the package, not the caller, so the order
  keeps the type it was placed under even if the package is reclassified later.
- **UI** — `NewOrderComponent`: `FilteredPackages` computed from the loaded
  `PackageDetailsDTO` list; changing screening type clears a package that no longer
  matches; the DOB picker syncs `DateTime?` ↔ the DTO's `DateOnly?`; the submit
  confirmation and success snackbar say "order created" instead of "invitation
  emailed" for Data screening; the post-submit reset returns the whole form
  (including screening type) to its initial state. New scoped CSS: `.ats-identity-grid`
  (3-col, collapses at 768px) and `.ats-screening-note` (token colors only).

## Bulk invite

The bulk tab gained the same Package section: screening type first, package select
disabled until it is chosen, and the same three-state note under the section.

- **The note is the same component on both tabs**, in all three states (nothing
  chosen / Manual / Data), sharing `ScreeningNoteIconFor` and `ScreeningNoteTitleFor`;
  only the body copy differs, because the choice changes per-candidate form fields on
  one tab and CSV columns on the other. The bulk copy names the **CSV headers**
  (`DateOfBirth`, `SSSNumber`, `TINNumber`) rather than the form labels, since that is
  what the operator types into a spreadsheet and what the upload check rejects on.
- Clicking the locked package select blinks the note on both tabs (the wrapper takes
  the click a disabled input never raises). One `isScreeningNoteBlinking` flag serves
  both, since only one tab renders at a time; switching tabs cancels a running blink so
  it cannot flash a note the user did not click.

- **On the file** — `BulkUploadFileDetailsDTO.AutoChasing` is posted as a form field
  and stored on `BulkUploadFileDetails`. `InsertBulkSubjectAsync` cross-checks it
  against the resolved package exactly as the single path does, then snapshots the
  **package's** classification onto the file.
- **CSV columns** — a data file must lead with the five standard columns followed by
  `DateOfBirth` (MM/dd/yyyy), `SSSNumber`, `TINNumber`. A manual file stops at
  `MobileNumber`; identity columns on a manual file are debris and are ignored, like
  any other trailing column.
- **Two places enforce this, deliberately.** `CsvPreviewParser.Parse(csv,
  requiresIdentity)` checks the header set before upload so the operator gets one
  snackbar naming the missing columns; `BulkSubmissionProcessorService` repeats the
  check server-side, because the file could have been posted without the UI. CsvHelper's
  own `HeaderValidated`/`MissingFieldFound` are disabled — the expected column set is
  screening-type-dependent, which CsvHelper cannot express — and this per-type header
  check replaces them.
- **Per row** — when the file is Data, `BulkSubjectRowValidator.ValidateIdentity`
  applies the same rules as the web validator (DOB in the past, SSS 10 digits, TIN
  9–12). A failing row is rejected individually and reported on the file's row
  outcomes; the file's good rows still import.
- **On each order** — the created `EmailInvitationRequest` carries the file's
  `AutoChasing` and the row's parsed identity values.

## The email invitation worker

`GetPendingEmailInvitationRequestsAsync` claims only `WHERE "AutoChasing" IS TRUE`.
A data order already holds the candidate's identity, so there is nothing to ask them
for and no form to send. `NULL` is excluded along with `false`: an unclassified order
cannot prove it is manual, and emailing a data candidate an application form is the
worse of the two mistakes. Legacy rows predating the column would therefore never be
emailed, which is exactly what `20260914160000_BackfillLegacyAutoChasing` prevents.

## No email means no email status

The worker's claim filter was necessary but not sufficient. **The single-order path
never went through the worker at all** — `InsertEmailInvitationRequestAsync` sent the
application form inline, inside the create transaction, unconditionally. A data order
placed through New Order was emailed a form the screening type exists to avoid, and
came out of the transaction stamped `EmailSentStatus = Done`. Migration
`20260914170000` clears those rows.

The send is now guarded by the **snapshotted** `AutoChasing` (the package's value, not
the caller's claim), and a data order is written with **every email column unset**:

| Column | Manual | Data |
|---|---|---|
| `EmailSentStatus` | `Pending` → `Done` after the inline send | `NULL` |
| `EmailSentAt` | set by the send | `NULL` |
| `EmailClaimedAt` | set when the worker claims it | `NULL` |
| `EmailSendAttempts` | incremented per attempt | `0` |

`NULL`, not `Pending`. `Pending` is a claim about queue position — "sent shortly" —
and it would be false in both directions: it tells a requestor an invitation is on its
way, and it describes a place in a queue the row does not hold, because the worker's
`AutoChasing IS TRUE` filter would never advance it. It would sit at `Pending`
forever and be counted as in-flight on every dashboard. `NULL` says "not applicable",
which is what is actually true.

`EmailSendAttempts` is the one exception: it is a non-nullable `int` on the entity, so
it stays `0` rather than `NULL`. Zero attempts is already the honest value.

`20260914170000_ClearEmailColumnsForDataScreening` makes `EmailSentStatus` nullable
(it was `NOT NULL`, which is why data orders had no choice but `Pending`) and clears
all four columns together on `AutoChasing IS FALSE` rows — a `NULL` status beside a
populated `EmailSentAt` would read as corruption. It excludes `NULL` deliberately:
`20260914160000` classified every legacy row as manual, so anything still `NULL`
arrived after that and cannot prove it is a data order.

**Bulk was already correct about the send** — its rows go through the worker, which
filters them out — but it too wrote `Pending`, so `BulkSubmissionProcessorService`
now writes `EmailSentStatus = file.AutoChasing is true ? Pending : null`. Both paths
write the same table and must agree.

### Resend

`ResendApplicationFormAsync` takes a caller-supplied id and is reachable from more
than one screen, so hiding the dialog button is not enough — it rejects anything that
is not manual with a `BadRequestException`, after the scope check. `AutoChasing is
not true` covers `NULL` as well as `false`, matching the worker's claim: an
unclassified order cannot prove it is manual.

### Reading a NULL status

Four surfaces render the status, and an unhandled `NULL` is misleading on each:

- **Subjects dialog badge** — `null or ""` maps to "Not sent" with a dashed, muted
  `is-none` style. Without it the `_` arm catches `NULL` and shows "Unknown", which
  means "a status outside the vocabulary" and reads as an anomaly.
- **Resend button** — hidden for a blank status, with
  `GetResendBlockedReason` explaining it is a data screening order.
- **Bulk dashboard email column** — a data file shows an em dash instead of a
  progress bar. `AutoChasing` is plumbed through `BulkUploadRowDTO` →
  `BulkUploadListDTO` → the UI DTO for this; the bar's permanent `0/N` would read as
  a stalled queue.
- **Bulk dashboard Screening column** — the dash needs an explanation on the same
  row, so the board carries a **Screening** column (Manual / Data / Not set) beside
  Type. Same vocabulary and null handling as the package management board, so a file
  and the package it was placed under read identically. `Not set` is dashed like the
  "Not sent" badge: both mean "no value applies", and neither resolves on its own.
- **Subjects dialog subtitle** — appends "Manual screening" / "Data screening" after
  package and order type. Without it, a data file opens onto a list where every badge
  reads "Not sent" and nothing on screen says why. Omitted when unknown; the board's
  Screening column already reports that case.
- **CSV export** — writes `"Not Applicable"`, because an empty cell in a spreadsheet
  reads as missing data.

## A migration that grew after it shipped

`20260914150000` originally added only the four `EmailInvitationRequest` columns; the
`BulkUploadFileDetails.AutoChasing` column was appended to the same file in a later
phase of the work. A database that had already applied the earlier version records the
migration id and **never re-runs it**, so it ends up without a column the model
declares — and the bulk claim query, which is `FromSqlRaw(... RETURNING t.*)`, throws:

> `The required column 'AutoChasing' was not present in the results of a 'FromSql'
> operation.`

`20260914160000` repairs that with `ADD COLUMN IF NOT EXISTS`, which is a no-op on a
database that applied the complete `150000`. **The lesson: once a migration may have
run anywhere, add a new one rather than editing it.** Raw-SQL `FromSql` queries make
the failure loud, because EF materialises the entity from the result set by column
name and cannot fall back to a default.

## How to verify

1. Apply the migrations; existing orders read `NULL` in `DateOfBirth`, `SSSNumber` and
   `TINNumber`, and `true` in `AutoChasing` (backfilled as manual). Confirm the bulk
   table has the column:
   `\d ats."BulkUploadFileDetails"` shows `AutoChasing | boolean`.
2. New Order → Package section is first; package select disabled until a screening
   type is chosen; the note text switches with the selection. Same on **both** tabs:
   switch to Bulk invite with nothing chosen and the note reads "Screening type",
   then changes to Manual/Data copy naming the CSV columns. Clicking the locked
   package select blinks the note on either tab.
3. Data + missing identity fields → the form blocks; bypassing the UI returns 400
   `ValidationException` from the API. Manual → the three fields optional.
4. Submitting Data with valid fields persists all four columns; a screening type
   that mismatches the package's classification returns 400.
5. Bulk: upload the standard template against a Data package — blocked before upload
   naming `DateOfBirth, SSSNumber, TINNumber`. Upload a data file with those columns
   — orders are created carrying the identity values, and **no invitation email is
   sent**. The same file against a Manual package sends invitations and leaves the
   identity columns null.
6. Place a single Data order and check the row: all of `EmailSentStatus`,
   `EmailSentAt` and `EmailClaimedAt` are `NULL`, `EmailSendAttempts` is `0`, and
   **no email arrives**. `OrderStatus` is still `Pending Candidate Info` and
   `TicketStatus` is `Pending` — only the candidate-facing email is suppressed, not
   the order or its OMS ticket.
   ```sql
   SELECT "AutoChasing", "EmailSentStatus", "EmailSentAt", "EmailClaimedAt",
          "EmailSendAttempts", "TicketStatus"
   FROM ats."EmailInvitationRequest" ORDER BY "OrderCreatedAt" DESC LIMIT 1;
   ```
7. Open that order's file in the subjects dialog: the badge reads **Not sent**
   (dashed/muted, not "Unknown"), and the Resend button is absent with the tooltip
   naming data screening. The dialog subtitle names the screening type. The bulk
   dashboard shows **Data** in the Screening column and an em dash in Emails, not a
   `0/N` bar. Export the file — the Email Sent Status column reads `Not Applicable`.
8. On the bulk board, a manual file reads **Manual** with a progress bar, and a file
   uploaded before screening types existed reads **Not set** in a dashed pill. Check
   the loading skeleton and the "no uploads" empty state still span the full table —
   both are driven by `ColumnCount`, which must match the header count (8).
9. Resend against a data order id via the endpoint directly → 400, and the row's
   `HashToken` is unchanged.
10. Tests: `dotnet test Test/Test/Test.csproj --filter
   "FullyQualifiedName~InsertEmailInvitationRequest|FullyQualifiedName~OrderInputValidator|FullyQualifiedName~Bulk|FullyQualifiedName~EmailNotification|FullyQualifiedName~CsvPreviewParser|FullyQualifiedName~ResendApplicationForm"`.

## What not to do

- Do not make the four columns NOT NULL — bulk creation
  (`BulkSubmissionProcessorService`), the public API and the seed data create rows
  without them.
- Do not move the Data-mode required rules into `EndorsementSubmissionService` —
  that would break the public API contract, whose callers cannot send the fields.
- Do not store the caller's claimed screening type; always snapshot the package's
  own `AutoChasing` (the cross-check exists to reject disagreement, not trust it).
- Do not let `NULL`-classified packages match a screening-type filter.
- Do not relax the email worker's claim to `AutoChasing IS NOT FALSE`. Excluding
  `NULL` is the point: it keeps an unclassified order out of the send queue instead
  of mailing an application form to a candidate who was never meant to get one.
- Do not add the identity columns to the shared bulk template. The manual template is
  the default, and a manual file that carries them still imports — the columns are
  required only when the file's own screening type is Data.
- Do not edit `20260914150000` (or any other already-released migration) to add a
  column. Databases that recorded it will not re-run it, and the missing column only
  surfaces later as a `FromSql` materialisation failure. Add a new migration.
- Do not give `20260914160000` a working `Down`. Reverting the backfill would also
  unclassify orders a user genuinely marked manual.
- Do not re-enable CsvHelper's `HeaderValidated`/`MissingFieldFound` in
  `BulkSubmissionProcessorService` — they cannot express a column set that depends on
  the file's screening type, and turning them on rejects every manual file.
- Do not give a data order `EmailSentStatus = Pending` to avoid the `NULL`. Nothing
  ever advances it: the worker claims `AutoChasing IS TRUE`, so the row would sit at
  `Pending` permanently and be counted as an invitation still in flight.
- Do not restore `EmailSentStatus` to `NOT NULL`. That constraint is the reason data
  orders were mislabelled in the first place.
- Do not rely on the worker's claim filter alone to suppress the send. The single
  order path emails inline inside the create transaction and never reaches the
  worker — that is the bug `20260914170000` cleaned up after.
- Do not guard the send on `emailInvitationRequestDTO.AutoChasing`. Use the value
  snapshotted from the package; the caller's claim is the thing being validated, not
  the authority.
- Do not treat a blank `EmailSentStatus` as "Unknown" in the UI. Unknown means a
  status outside the vocabulary and signals an anomaly; blank means no email applies.
- Do not add or remove a column on the bulk board without updating `ColumnCount` on
  its `TableComponent`. It drives the loading skeleton and the empty-state colspan,
  and a stale value only shows up as a short skeleton row while data is loading.
- Do not render an unclassified file as "Manual" on the board. The label follows the
  same rule as everything else here: `NULL` is neither type.
