# Package ID migration — database runbook

Orders now reference their package by **id** rather than by name. This is what you need to
do to each database. Written to be followed without reading the C#.

**Migration:** `20260830124343_AddPackageIdToOrdersATSMigration`

---

## What changes

Two tables gain a `PackageId` column with a real foreign key to `ats."PackageDetails"`:

| Table | New column | Existing column |
|---|---|---|
| `ats."EmailInvitationRequest"` | `PackageId` → FK | `SelectPackage` stays |
| `ats."BulkUploadFileDetails"` | `PackageId` → FK | `PackageType` stays |

The name columns are **not** dropped. They stay as display labels, read by the report
lists, search, exports and the ticketing screen. `PackageId` is the relationship; the name
is what people see.

`ON DELETE RESTRICT` on both — deleting a package that orders reference is refused rather
than deleting the orders.

---

## Why

The order → package link was a string comparison. Renaming a package silently orphaned
every order that referenced it: the order kept the old string, the OMS ticketing join
stopped matching, and the order parked as `Error` with a reason nobody could act on.

With the foreign key, a rename is safe — and `EditPackageAsync` now refreshes the display
label on affected orders so the two never disagree.

---

## Before you run it

The migration adds `PackageId` as `NOT NULL` with a default of `0`. **No package has id 0**,
so if any order rows exist, the foreign key will fail.

Run this first:

```sql
SELECT
    (SELECT COUNT(*) FROM ats."EmailInvitationRequest") AS orders,
    (SELECT COUNT(*) FROM ats."BulkUploadFileDetails")  AS bulk_files;
```

**If both are 0** → nothing to do, go to *Running it*.

**If either is non-zero** → the database has order data. Since none of this is in
production, the expected path is to drop and recreate:

```sql
TRUNCATE TABLE
    ats."EmailInvitationRequest",
    ats."BulkUploadFileDetails"
RESTART IDENTITY CASCADE;
```

`CASCADE` also clears the dependent rows — `PersonalDetails`, `AddressDetails`,
`OrderStatusHistory`, `ApplicantSearchProjection` and the rest. That is intended: an order
without its child records is not useful.

> If you would rather keep the data, do not run the migration as-is. It needs a backfill
> step instead — see *Keeping existing data* at the bottom.

---

## Running it

```powershell
dotnet ef database update `
  --context ATSDBContext `
  --project BackendAPI/API/APIs/APIs.csproj `
  --startup-project BackendAPI/API/APIs/APIs.csproj
```

Or just start the API — `ATSDatabaseExtensions.ATSIntializeDatabaseAsync` runs
`Database.MigrateAsync()` on boot.

The seed then repopulates. Note that seeded orders are **only** created for package names
that exist in `PackageDetails`; a seed row whose package is absent is skipped rather than
failing the whole seed.

---

## After

**1. Every row has a real package:**

```sql
SELECT COUNT(*) AS orphaned
FROM ats."EmailInvitationRequest" e
LEFT JOIN ats."PackageDetails" p ON p."PackageId" = e."PackageId"
WHERE p."PackageId" IS NULL;
```

Must be `0`. The foreign key guarantees it, so a non-zero result means the constraint was
not created — check the migration actually applied.

**2. The label agrees with the package it points at:**

```sql
SELECT e."EmailInvitationID", e."SelectPackage", p."PackageName"
FROM ats."EmailInvitationRequest" e
JOIN ats."PackageDetails" p ON p."PackageId" = e."PackageId"
WHERE e."SelectPackage" IS DISTINCT FROM p."PackageName"
LIMIT 20;
```

Should return nothing. If rows appear, a rename happened without the label refresh —
report it, it means `RelabelPackageOnOrdersAsync` did not run.

**3. Constraints exist:**

```sql
SELECT conname, conrelid::regclass AS table_name
FROM pg_constraint
WHERE conname LIKE '%PackageDetails_PackageId%';
```

Expect two rows, one per table.

---

## Rolling back

```powershell
dotnet ef database update <previous-migration-name> `
  --context ATSDBContext `
  --project BackendAPI/API/APIs/APIs.csproj `
  --startup-project BackendAPI/API/APIs/APIs.csproj
```

The `Down` drops the foreign keys, indexes and columns. `SelectPackage` and `PackageType`
were never modified, so no data is lost — the application simply goes back to matching by
name.

---

## Order of environments

Local → Sandbox → UAT. Run the pre-flight count on each: they hold different data, and a
database that is empty locally may not be elsewhere.

---

## Keeping existing data

If a database has orders worth preserving, replace the generated `Up` with a backfill
before the constraint is added. The shape, mirroring what
`20260807144902_UpdatedClientPackageRelationships` already does:

```sql
-- 1. add the column nullable, not NOT NULL
ALTER TABLE ats."EmailInvitationRequest" ADD COLUMN "PackageId" integer NULL;

-- 2. match on the name; PackageName is unique, so this is 1:1
UPDATE ats."EmailInvitationRequest" e
SET "PackageId" = p."PackageId"
FROM ats."PackageDetails" p
WHERE p."PackageName" = e."SelectPackage";

-- 3. park anything that did not match on a placeholder
INSERT INTO ats."PackageDetails"
    ("PackageName", "PackageDescription", "IsActive", "FollowUpEmail", "CreatedAt", "UpdatedAt")
VALUES ('Legacy Unassigned', '0', FALSE, 0, NOW(), NOW())
ON CONFLICT ("PackageName") DO NOTHING;

UPDATE ats."EmailInvitationRequest"
SET "PackageId" = (SELECT "PackageId" FROM ats."PackageDetails" WHERE "PackageName" = 'Legacy Unassigned')
WHERE "PackageId" IS NULL;

-- 4. only now tighten
ALTER TABLE ats."EmailInvitationRequest" ALTER COLUMN "PackageId" SET NOT NULL;
```

Then repeat for `ats."BulkUploadFileDetails"` / `PackageType`.

Before doing that, size the problem — the count decides whether it is worth the effort:

```sql
SELECT e."SelectPackage", COUNT(*) AS orphaned
FROM ats."EmailInvitationRequest" e
LEFT JOIN ats."PackageDetails" p ON p."PackageName" = e."SelectPackage"
WHERE p."PackageId" IS NULL
GROUP BY e."SelectPackage"
ORDER BY orphaned DESC;
```

Rows landing on `Legacy Unassigned` are real orders whose package no longer exists under
that name. They are visible rather than silently broken: the ticketing screen reports them
as `Error`. Reassign them with a targeted `UPDATE` when you know the right package.
