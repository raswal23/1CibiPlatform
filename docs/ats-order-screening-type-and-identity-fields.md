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
   claims `AutoChasing IS TRUE` rows and nothing else.

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
6. Tests: `dotnet test Test/Test/Test.csproj --filter
   "FullyQualifiedName~InsertEmailInvitationRequest|FullyQualifiedName~OrderInputValidator|FullyQualifiedName~Bulk|FullyQualifiedName~EmailNotification|FullyQualifiedName~CsvPreviewParser"`.

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
