# ATS Package Screening Type (AutoChasing)

**Date:** 2026-09-14
**Scope:** ATS Package Management — database, backend contracts, Add/Edit dialogs

## What it does

Each screening package now declares its **screening type**: **Manual** (the screening
team chases the checks) or **Data** (processed automatically). Admins choose it in the
Add Package and Edit Package dialogs from a "Screening type" select with exactly those
two options.

## How it is stored

The column is `ats."PackageDetails"."AutoChasing"`, a nullable `boolean`:

| UI selection | `AutoChasing` in DB |
|---|---|
| Manual | `true` (1) |
| Data | `false` (0) |
| Not set (never chosen) | `NULL` |

The field is deliberately named `AutoChasing` in the schema and contracts while the UI
labels it "Screening type" — the business term for the same switch. Existing rows stay
`NULL` and render as "Not set" — they are deliberately not silently classified as Data.

## How it works

The value rides the existing package vertical slice end to end; no new endpoints:

- **Entity/config** — `BackendAPI/Modules/ATS/Data/Entities/PackageDetails.cs`,
  `Data/EntityConfiguration/PackageDetailsConfiguration.cs` (`IsRequired`).
- **Migration** — `BackendAPI/API/APIs/Migrations/ATS/20260914120000_AddAutoChasingToPackageDetails.cs`.
  Hand-written because the EF CLI could not run in the working environment: the
  migration class carries the `[DbContext]`/`[Migration]` attributes a Designer file
  would normally hold, and `ATSDBContextModelSnapshot.cs` was updated in the same
  change. If you later regenerate migrations with the CLI, this pair must stay
  consistent.
- **DTOs** — backend and UI `PackageDetailsDTO`, `AddPackageDTO`, `EditPackageDTO`
  all carry `bool AutoChasing`.
- **Repository** — `ATSRepository.Packages.cs` projects it in `GetPackagesPageAsync`
  and persists it in `AddPackageAsync`; `PackageManagementService.EditPackageAsync`
  copies it onto the tracked entity like the other editable fields.
- **UI** — `AddPackageComponent.razor` / `EditPackageComponent.razor` gained a
  "Screening type" `MudSelect` in the "Timing & availability" section, above
  Follow up email. It reuses the dialogs' existing `ap-`/`ep-` field classes
  (`ap-field`, `ap-input-wrap`, `ap-control`, `ap-hint`) so it inherits the theme —
  no new CSS was added. `EditPackageComponent.razor.cs` maps the value into the
  edit DTO in `OnParametersSet`.
- **Table** — `PackageManagement.razor` shows a "Screening Type" column between
  Description and Follow Up Email, rendered with the shared
  `.ats-management-email-badge` pill (clock icon for Manual, database icon for
  Data, question-mark icon for Not set). `ColumnCount` is 8 and the
  `.ats-package-card` header width ladder in `wwwroot/css/ats.css` was rebalanced
  for the extra column.

## How to verify

1. Apply the migration (`ATSInitializeDatabaseAsync` runs `MigrateAsync` on startup)
   and confirm the column: existing packages read `AutoChasing = NULL` and list as
   "Not set".
2. Add a package with Screening type "Manual" → row saves with `AutoChasing = true`.
3. Edit it to "Data" → row updates to `false`; the dialog re-opens showing "Data".
4. Tests: `dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~PackageManagement"` —
   the Add/Edit integration tests assert the round-trip of `AutoChasing`.

## What not to do

- Do not rename the column or DTO property to match the UI label; public API clients
  and the UI JSON contract bind to `autoChasing`.
- Do not backfill `NULL` rows to a chosen value in a migration — "Not set" is
  information (nobody has classified that package yet), and workflows that later
  consume `AutoChasing` must treat `NULL` as "unclassified", not as Data.
