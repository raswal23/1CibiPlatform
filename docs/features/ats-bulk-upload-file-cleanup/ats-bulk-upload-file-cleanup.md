# ATS Bulk Upload File Cleanup — Implementation Review

Removes file keys from object storage for bulk upload files that have been processed and have rejected rows. Runs daily at 1 AM to maintain storage efficiency.

Branch: `feature/ATS-Bulk-Upload-File-Cleanup`. Follows `docs/feature-development-guide.md`.

---

## 1. What was decided, and why

### Trigger point: Scheduled cleanup

The cleanup runs daily at 1 AM via a Quartz job. It identifies bulk upload files where:

1. Status is `Done` (processing completed)
2. RejectedRows is not null (some rows were rejected during processing)
3. FileKey is not null (the original file still exists in object storage)

This ensures that processed files with rejected rows are cleaned up regularly, preventing accumulation of unnecessary files in object storage.

### No new table

The cleanup uses existing `ats.BulkUploadFileDetails` table. The job selects records matching the criteria and removes their associated files from object storage, then clears the `FileKey` column.

### Quartz job, not `BackgroundService`

Following the ATS background job pattern, the cleanup uses Quartz with a cron trigger (`0 0 1 * * ?` = daily at 1 AM). Quartz is already registered with a persistent, **clustered** Postgres store (`ats.qrtz_*`, `UseClustering()`), so this is safe across API instances.

---

## 2. Step by step

### Step 1 — Repository method for cleanup query

`Data/Repository/BulkUploads/IBulkUploadRepository.cs` plus implementation in `Data/Repository/BulkUploads/ATSRepository.BulkUploads.cs`:

Adds methods to find records for cleanup:

| Method | Purpose |
|---|---|
| `GetBulkUploadFileDetailsForCleanupAsync` | Retrieves up to 50 records where `Status = Done`, `RejectedRows != null`, `FileKey` is not null, and `IsFileKeyDeleted = false` |
| `ClearBulkUploadFileKeyAsync` | Clears the `FileKey` column and sets `IsFileKeyDeleted = true` after successful deletion |

The query filters on:
- `x.Status == BulkFileStatus.Done` (processing completed)
- `x.RejectedRows != null` (some rows were rejected)
- `!string.IsNullOrEmpty(x.FileKey)` (file still exists in storage)
- `x.IsFileKeyDeleted == false` (file key has not been marked as deleted)

### Step 2 — Quartz job implementation

`BackgroundJobs/BulkUploadFileCleanup/BulkUploadFileCleanupJob.cs` — implements `IJob` with `[DisallowConcurrentExecution]`:

| Responsibility | Implementation |
|---|---|
| Scope management | Creates a new DI scope per execution to resolve dependencies |
| File deletion | Iterates records, deletes from object storage using `IObjectStorageService.DeleteAsync` |
| Database update | Clears `FileKey` column after successful deletion |
| Error handling | Logs errors per file but continues processing remaining files |
| Logging | Comprehensive logging for monitoring and debugging |

### Step 3 — Job scheduling

`BackgroundJobs/BulkUploadFileCleanup/BulkUploadFileCleanupServiceSetup.cs` — implements `IConfigureOptions<QuartzOptions>`:

| Configuration | Value | Reason |
|---|---|---|
| Job identity | `nameof(BulkUploadFileCleanupJob)` | Standard naming pattern |
| Trigger identity | `"BulkUploadFileCleanupTrigger"` | Descriptive trigger name |
| Schedule | `WithCronSchedule("0 0 1 * * ?")` | Daily at 1:00 AM |
| Concurrent execution | `[DisallowConcurrentExecution]` | Prevents overlapping runs |

### Step 4 — Service registration

`ServiceConfig/ATSServiceConfiguration.cs` — registers the job setup:

```csharp
services.ConfigureOptions<BulkUploadFileCleanup.BulkUploadFileCleanupServiceSetup>();
```

Follows the same pattern as other ATS background jobs.

### Step 5 — Caching layer

`Data/Cache/BulkUploads/ATSCacheRepository.BulkUploads.Cache.cs` — adds caching implementations:

Implements the new repository methods to maintain consistency with the caching pattern used by other ATS repositories.

---

## 3. How it works

### The cleanup process

1. **Query**: Job retrieves up to 50 records matching cleanup criteria
2. **Iteration**: For each record with a `FileKey`:
   - Delete file from object storage
   - If successful, clear `FileKey` in database
   - Log success/error per file
3. **Logging**: Reports total processed, successful, and failed deletions

### Error resilience

- **Per-file errors**: If one file fails to delete, the job continues with remaining files
- **Database consistency**: Only clears `FileKey` after successful object storage deletion
- **Logging**: Detailed logging enables monitoring and troubleshooting

### Storage efficiency

- **Batch size**: Limits to 50 records per run to manage memory and processing time
- **Timing**: Runs at 1 AM when system load is typically lowest
- **Criteria**: Only processes files that meet specific business conditions

---

## 4. How to verify

1. Create bulk upload files with status `Done` and non-null `RejectedRows`
2. Confirm files exist in object storage with valid `FileKey` values
3. Wait for 1 AM or trigger the job manually in development
4. Verify files are deleted from object storage
5. Confirm `FileKey` columns are cleared in `ats.BulkUploadFileDetails`
6. Check application logs for cleanup process execution

```sql
-- Check records before cleanup
SELECT "FileID", "FileName", "Status", "RejectedRows", "FileKey"
FROM ats."BulkUploadFileDetails"
WHERE "Status" = 'Done' AND "RejectedRows" IS NOT NULL AND "FileKey" IS NOT NULL;

-- Check records after cleanup
SELECT "FileID", "FileName", "Status", "RejectedRows", "FileKey"
FROM ats."BulkUploadFileDetails"
WHERE "Status" = 'Done' AND "RejectedRows" IS NOT NULL AND "FileKey" IS NULL;
```

### Tests

```csharp
// Verify the query logic
// Verify file deletion calls
// Verify database updates after successful deletion
// Verify error handling and logging
// Verify the cron scheduling
```

---

## 5. What not to do

- Do not modify the cron schedule without considering system load patterns
- Do not increase the batch size significantly without testing performance impact
- Do not remove the `[DisallowConcurrentExecution]` attribute without addressing potential race conditions
- Do not bypass the object storage deletion and only clear the database - this creates orphaned files
- Do not process records that don't meet the exact criteria (Done status + RejectedRows + FileKey)
- Do not skip logging for individual file errors - monitoring depends on complete logs

---

## 6. Maintenance considerations

- **Storage growth**: Monitor object storage usage to verify cleanup effectiveness
- **Performance**: Track job execution time as the number of bulk uploads increases
- **Error rates**: Monitor for patterns in file deletion failures
- **Schedule conflicts**: Ensure the 1 AM time remains appropriate as the system scales
- **Dependencies**: Verify `IObjectStorageService` availability and error handling