# Employment Verification — console redesign and contact directory

Two changes that shipped together: the Employment Verification console was rebuilt to
look and behave like a sibling of the ATS console, and a new **Contacts** tab was added
holding a directory of HR mailboxes, seeded with 7,357 rows from a client-supplied
workbook.

## What it does and why

### The problem

Employment Verification is one of eight applications on 1CibiPlatform, but it did not
read as one of them.

It was a **single full-bleed page** on `ConsoleLayout` — no topbar, no breadcrumb, no
avatar, no dark-mode toggle, no navigation of any kind. Its only navigational affordance
was a fixed "Back to OnePlatform" tab clipped to the left edge of the viewport. Its two
views were a private `_activeView` field behind a segmented control, so a tab could not
be linked to, bookmarked, or reached with the browser's back button.

And it was **magenta** (`--c-ev-accent: #d9468d`) while every other application on the
platform is navy and blue. A user clicked a blue card on the home grid and landed on a
pink page. That undercuts exactly the thing the platform exists to demonstrate: several
distinct applications that visibly belong to one product.

### What changed

**A top navbar instead of no navigation.** `Layout/EVLayout.razor` gives the module the
same shell grammar as ATS — the same navy `--c-sidebar-gradient`, the same
`.ats-console-icon-btn` controls, the same avatar — arranged horizontally. The axis is
the deliberate difference: ATS has a sidebar because it has sixteen modules, EV has a
navbar because it has three tabs. You can tell which application you are in at a glance
without either looking foreign.

**Real routes instead of a private field.** The two views became
`/employmentverification/requests` and `/employmentverification/tracking`, joined by the
new `/employmentverification/contacts`. Each is a page with its own `@page` directive,
so tabs deep-link, bookmark and respond to the back button. The old
`/employmentverification/verification` route survives as a redirect — see *Why the old
route is still there* below.

**Teal instead of magenta.** The `--c-ev-*` tokens were re-pointed at a teal-cyan accent
(`#0e7490` / `#0891b2`) drawn from colours already in the brand: the brand bar's
`#68c0d6` stop and the logo's `#7fc4e8` facet. The always-dark surfaces — navbar, dialog
headers, primary buttons — use the **same** shared navy tokens as ATS and were not
touched. Only the accent differs, which is what makes the two applications read as
siblings rather than as two products or as one product twice.

**A Contacts directory.** A straightforward CRUD table of company name and email
address, with add and edit dialogs styled identically to the ATS client dialogs. It
answers a concrete question staff had no good answer to: *which HR mailbox do I send
this verification request to?*

> **Now load-bearing, and it is an allow-list.** The directory was standalone when it
> shipped. The automatic sender
> ([employment-verification-auto-send.md](../employment-verification-auto-send/employment-verification-auto-send.md))
> will only write to a supervisor address that appears here — the address comes off the
> candidate's own application form, so the directory is what authorises it. Matched on the
> address, case-insensitively, via
> `IContactDirectoryRepository.GetKnownActiveMailboxesAsync`.
>
> Two consequences. **Adding or deactivating a row changes whether real mail is sent**, not
> merely where. And because this directory holds general company mailboxes while the form
> captures named individuals, most segments will not match until someone adds the address —
> the auto-send queue is the worklist for that.

## How it works

### The three tabs

| Route | Page | Data |
|---|---|---|
| `/employmentverification/verification` | `EmploymentVerificationLanding.razor` | redirect only |
| `/employmentverification/requests` | `NeedsRequest.razor` | in-progress ATS candidates without an open request |
| `/employmentverification/tracking` | `Tracking.razor` | requests raised from this module, and their outcomes |
| `/employmentverification/contacts` | `Contacts.razor` | the HR contact directory |

All three real pages carry `@layout EVLayout`, `@inherits CrudPageBase` and
`[RequirePermission(8, 9)]`.

### Why the old route is still there

`/employmentverification/verification` is the path registered as **submenu 9** in
`ShareData/Auth/SubMenuList.cs`, and `Home.razor.cs` builds the home application card's
link from it. The feature guide (§13a) requires the frontend catalogs, the route
attributes and the **backend permission seed data** to agree on the same numeric IDs.

Renaming that route would therefore have meant touching the submenu catalog and the API
seed data — a much wider and riskier change than it looks. Keeping it as a redirect to
the first tab means the home card, every existing bookmark, and application 8 / submenu 9
all keep working untouched.

### Tables

All three tabs now use the shared `TableComponent`, replacing a hand-written
`<table class="ev-table">`. That brings the debounced search field, the reload button,
skeleton loading rows and 960px card mode for free, and makes an EV table visually
identical to an ATS one.

The two request tabs page **client side**: their endpoints return the whole list in one
call, which is correct at their scale, and rewriting those endpoints was out of scope.
Contacts pages **server side** over a keyset cursor, because 7,357 rows is not a list you
hold in memory.

Status chips reuse the shared `.ats-status-pill` vocabulary rather than declaring an
EV-specific set — `Verified` maps to `done`, `Rejected` to `error`, `Sent` to
`processing` (the class whose dot pulses, which is right: it is the only actively
changing state).

### The review drawer stayed a drawer

The "send verification email" panel on the Needs request tab was **not** converted to a
MudDialog like the ATS CRUD screens. It is a read-and-send review panel rather than a
form, and it is the one surface where this application keeps its own character. Backdrop
clicks still deliberately do not close it — an accidental click outside should not
discard a request someone is reviewing. Escape and the explicit buttons do.

### The contact directory

`CompanyName` + `EmailAddress`, plus `Id`, `IsActive`, `CreatedAt` and `UpdatedAt`. The
module has no shared entity base class, so those are declared on the entity and stamped
by the service.

**Uniqueness is on the (company, mailbox) pair, not on the email alone.** All 7,357
emails in the source workbook happen to be unique, but that is a property of one
spreadsheet rather than a business rule. A shared HR inbox serving several BPO
subsidiaries is normal in this sector, and a unique index on the email would turn the
first legitimate one into a support ticket — or, worse, into someone typing a fake
address to get past the error. Compare `AtsEmailAccountConfiguration`, which justifies
*its* unique email with a concrete invariant about provider quotas; the directory has no
equivalent.

The duplicate check lives at three layers doing three different jobs: the **validator**
checks shape only and stays pure, the **service** does a check-then-write so the ordinary
case gets a `409` naming the company and mailbox, and the **unique index** is the
authority that actually holds under concurrency.

**Deactivation is the only removal.** There is no delete endpoint and the list does not
filter inactive rows, matching ATS module and client management exactly. With keyset
pagination a hidden-by-default filter makes the total count and the page contents
disagree in a way that is hard to explain, and someone who deactivates a row by mistake
would have no way to find it again.

### Seeding

The 7,357 rows ship as a `.sql` beside the assembly, executed by the migration that
creates the table. Ids are UUIDv5 over `lower(trim(email))`, so the seed is idempotent
and a refreshed workbook re-run produces the same id for every unchanged mailbox —
letting a later migration insert only what is genuinely new. `ON CONFLICT DO NOTHING`,
never `DO UPDATE`: these rows are editable through the console and a re-run must not
overwrite an operator's correction.

Regenerate with:

```powershell
./Tools/generate-ev-contacts-seed.ps1 `
    -WorkbookPath "<path>\Contact Database for Jobstreet.xlsx" `
    -OutputPath   "BackendAPI/API/APIs/Migrations/EmploymentVerification/EmploymentVerificationContactsSeed.sql"
```

The generator is deliberately fail-fast. It rejects an over-length name rather than
truncating it, and it rejects an interior `|` rather than escaping it — see the next
section for why that character matters.

Two source-data details worth recording. The workbook has **7,358** populated-looking
rows but one is an empty trailing row left by stray formatting, so 7,357 is correct. And
one company name ended `"PRIMESOFT PHILIPPINES, INC (PPI) |` — a trailing separator left
by whoever compiled the list. The generator strips a trailing `|` as unambiguous edge
noise but still hard-fails on an interior one, where the intent is genuinely unclear and
a human should decide.

### The `|` character is not cosmetic

`CursorCodec` encodes keyset cursors as `base64("field1|field2")` and splits on `|`,
returning `null` when the field count is wrong — by design, so a stale cursor degrades to
"first page" rather than erroring.

The contacts cursor is `(CompanyName, Id)`. A company name containing a `|` therefore
makes every cursor minted from that row decode as `null`, silently pinning the user to
page 1 from that row onward with no error anywhere. Both command validators reject it,
and the dialog rejects it client side too. Fixed at the edge rather than by escaping
inside the shared codec, which every other keyset screen in the platform depends on.

### Why the cursor is composite

Company name is not unique — CONCENTRIX alone has 180 mailboxes in the seed data. A
single-field cursor would skip or repeat rows at every company boundary, so the cursor
carries `(CompanyName, Id)` and the `ORDER BY` matches the
`IX_EmploymentVerificationContacts_CompanyName_Id` index exactly.

## How to verify it

```powershell
dotnet build 1CibiPlatform.sln
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~EmploymentVerification.UnitTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~IntegrationTests"
dotnet run --project UI/FrontendWebassembly     # http://localhost:5134
```

Confirm the seed landed and is idempotent:

```sql
SELECT count(*) FROM employment_verification."EmploymentVerificationContacts";   -- 7357
```

Re-running the seed script must report `INSERT 0 0` for every batch.

Confirm the gateway picked up the three new routes — `GET /__routes` should list
`GetEmploymentVerificationContacts`, `AddEmploymentVerificationContact` and
`EditEmploymentVerificationContact`, and the startup log's
`[Gateway] Collected N routes` count should be three higher than before.

Manually: all four routes at **390px** and in **both light and dark mode**, plus the
601–960px band where `MudTable` card mode fires; keyboard tab order through the navbar;
the home card and the bare `/verification` URL both landing on Needs request.

## What not to do

- **Do not add a unique index on `EmailAddress` alone.** The shared-mailbox case is
  legitimate and the pair index is the real invariant.
- **Do not add a delete endpoint.** Deactivation is the removal model, matching ATS.
- **Do not let a company name carry a `|`.** It breaks the keyset cursor silently.
- **Do not change the seed's namespace GUID** in `generate-ev-contacts-seed.ps1`. Every
  committed id derives from it; a new value re-keys all 7,357 rows and the next migration
  would insert the whole file again as duplicates.
- **Do not embed the seed `.sql` as a resource.** It was tried; the ~950 KB resource
  nearly doubled `APIs.dll` and tripped Smart App Control on at least one developer
  machine, which then blocked the test host from loading the assembly at all. It ships as
  `Content` copied to the output directory, matching `Scripts/quartz_postgres.sql`.
- **Do not make the dialog's email check stricter than the server's.** FluentValidation's
  `EmailAddress()` accepts a dotless domain; a client that blocks what the API would have
  accepted is worse than one that lets the 400 through. This is pinned by a test.
- **Do not add a filter to the contacts table without adding it to the
  `CursorTableLoader` signature** *and* the cache keys. The loader resets its cursor stack
  only when the signature changes, so a filter left out of it keeps walking cursors minted
  under the old predicate and silently skips rows.

## Related

- [Code walkthrough](employment-verification-contact-directory_code_explanation.md)
- [Request tracking](../transaction-runner/employment-verification-request-tracking/employment-verification-request-tracking.md)
  — the feature whose pages this change restructured
- [UI theming and responsiveness](../../ui-theming-and-responsiveness.md)
