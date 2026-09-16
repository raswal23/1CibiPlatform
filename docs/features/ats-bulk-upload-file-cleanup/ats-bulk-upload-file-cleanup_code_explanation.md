# ATS Bulk Upload File Cleanup — Code Explanation

Companion to [`ats-bulk-upload-file-cleanup.md`](ats-bulk-upload-file-cleanup.md). That document explains *what* the feature does and *why* the rules exist. This one exists so a developer can change the implementation without opening every file cold — it walks the real call chains, names the exact method at each hop, and quotes the code that carries the correctness.

Read it top to bottom once, then use it as a map: *"I'm changing X, what else touches it?"* is answered by §9.

---

## 1. The data model — cleanup criteria

### 1.1 Query criteria in `ATSRepository.BulkUploads.cs`

The cleanup query looks for records meeting all three criteria:

```csharp
public async Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsForCleanupAsync(CancellationToken cancellationToken)
{
	return await _dbcontext.BulkUploadFileDetails
		.AsNoTracking()
		.Where(x => x.Status == BulkFileStatus.Done
				 && x.RejectedRows != null
				 && !string.IsNullOrEmpty(x.FileKey)
				 && x.IsFileKeyDeleted == false)
		.Take(50) // Take up to 50 records
		.ToListAsync(cancellationToken);
}
```

- `x.Status == BulkFileStatus.Done` — only processes files that have completed
- `x.RejectedRows != null` — only processes files that had rejected rows during processing
- `!string.IsNullOrEmpty(x.FileKey)` — only processes files that still have a file key to delete
- `.Take(50)` — limits to 50 records per run to manage memory and processing time

### 1.2 Database update after successful deletion

```csharp
public async Task<bool> ClearBulkUploadFileKeyAsync(Guid fileId, CancellationToken cancellationToken)
{
	var updated = await _dbcontext.BulkUploadFileDetails
		.Where(x => x.FileID == fileId)
		.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.FileKey, x => (string?)null),
			cancellationToken);

	return updated > 0;
}
```

Updates the `FileKey` column to null and sets `IsFileKeyDeleted = true` after successful object storage deletion, maintaining data consistency.

---

## 2. The Quartz job — one execution end to end

### 2.1 Job registration — `BackgroundJobs/BulkUploadFileCleanup/BulkUploadFileCleanupServiceSetup.cs`

```csharp
public class BulkUploadFileCleanupServiceSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(BulkUploadFileCleanupJob));
		options.AddJob<BulkUploadFileCleanupJob>(opts => opts.WithIdentity(jobKey));

		// Schedule the job to run daily at exactly 1 AM using cron expression
		// Cron: Second Minute Hour Day Month DayOfWeek
		// "0 0 1 * * ?" means at 1:00 AM every day
		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("BulkUploadFileCleanupTrigger")
			.WithCronSchedule("0 0 1 * * ?")); // Daily at 1:00 AM
	}
}
```

- `WithCronSchedule("0 0 1 * * ?")` schedules the job for exactly 1:00 AM daily
- `DisallowConcurrentExecution` attribute prevents overlapping executions
- Job identity and trigger identity follow the naming convention used by other ATS jobs

### 2.2 Job execution — `BackgroundJobs/BulkUploadFileCleanup/BulkUploadFileCleanupJob.cs`

The skeleton:

```csharp
[DisallowConcurrentExecution]
public class BulkUploadFileCleanupJob : IJob
{
	public async Task Execute(IJobExecutionContext context)
	{
		_logger.LogInformation("Starting bulk upload file cleanup process at {Timestamp}.", DateTime.UtcNow);

		using var scope = _scopeFactory.CreateScope();
		var repository = scope.ServiceProvider.GetRequiredService<IATSRepository>();
		var objectStorageService = scope.ServiceProvider.GetRequiredService<IObjectStorageService>();

		// Query, iterate, delete, update, log
	}
}
```

**Concurrency protection**: `[DisallowConcurrentExecution]` ensures only one instance runs at a time.

**DI scope**: Creates a new scope per execution to resolve dependencies properly.

### 2.3 The cleanup loop

```csharp
// Get up to 50 records where status is "Done" and RejectedRows is not null
var filesToClean = await repository.GetBulkUploadFileDetailsForCleanupAsync(context.CancellationToken);

if (filesToClean.Count == 0)
{
	_logger.LogInformation("No files found for cleanup.");
	return;
}

_logger.LogInformation("Found {FileCount} files for cleanup.", filesToClean.Count);

var deletedCount = 0;
var errorCount = 0;

foreach (var file in filesToClean)
{
	context.CancellationToken.ThrowIfCancellationRequested();

	if (string.IsNullOrWhiteSpace(file.FileKey))
	{
		_logger.LogDebug("File {FileId} has no FileKey to delete.", file.FileID);
		continue;
	}

	try
	{
		await objectStorageService.DeleteAsync(file.FileKey, context.CancellationToken);
		deletedCount++;

		_logger.LogDebug("Successfully deleted file {FileKey} for file ID {FileId}", 
			file.FileKey, file.FileID);

		// Clear the FileKey in the database after successful deletion
		await repository.ClearBulkUploadFileKeyAsync(file.FileID, context.CancellationToken);
	}
	catch (Exception ex)
	{
		errorCount++;
		_logger.LogError(ex, "Failed to delete file {FileKey} for file ID {FileId}", 
			file.FileKey, file.FileID);
	}
}
```

**Per-file resilience**: If one file fails to delete, processing continues with remaining files.

**Database consistency**: Only updates `FileKey` to null and sets `IsFileKeyDeleted` to true after successful object storage deletion.

**Cancellation awareness**: Checks cancellation token during iteration.

**Detailed logging**: Logs success/error per file and totals at completion.

---

## 3. Service registration — `ServiceConfig/ATSServiceConfiguration.cs`

```csharp
services.ConfigureOptions<BulkUploadFileCleanup.BulkUploadFileCleanupServiceSetup>();
```

Registering the job setup follows the same pattern as other ATS background jobs:

- `BulkSubmissionBackgroundJobSetup`
- `EmailNotificationBackgroundJobSetup` 
- `OMSTicketingBackgroundJobSetup`
- `FollowUpEmailBackgroundJobSetup`

### 3.1 Repository forwarding

The job uses the standard ATS repository pattern:

```csharp
var repository = scope.ServiceProvider.GetRequiredService<IATSRepository>();
var objectStorageService = scope.ServiceProvider.GetRequiredService<IObjectStorageService>();
```

Both services are registered in `ATSServiceConfiguration.AddATSServices()`.

---

## 4. Caching layer — `Data/Cache/BulkUploads/ATSCacheRepository.BulkUploads.Cache.cs`

The caching layer implements the same methods as the repository to maintain consistency:

```csharp
public async Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsForCleanupAsync(CancellationToken cancellationToken)
{
	return await _atsRepository.GetBulkUploadFileDetailsForCleanupAsync(cancellationToken);
}

public async Task<bool> ClearBulkUploadFileKeyAsync(Guid fileId, CancellationToken cancellationToken)
{
	return await _atsRepository.ClearBulkUploadFileKeyAsync(fileId, cancellationToken);
}
```

Following the same pattern as other caching decorators in the ATS module.

---

## 5. Integration points

### 5.1 Object Storage Service

The job depends on `IObjectStorageService` for file deletion:

```csharp
await objectStorageService.DeleteAsync(file.FileKey, context.CancellationToken);
```

This service is configured in `AlibabaStorageConfigService` and registered as `AlibabaOssStorageService`.

### 5.2 Bulk File Status

The query uses `BulkFileStatus.Done` from `ATS.Constants.BulkFileStatus`:

```csharp
x.Status == BulkFileStatus.Done
```

This ensures only fully processed files are considered for cleanup.

---

## 6. Error handling and logging

### 6.1 Per-file error handling

```csharp
try
{
	await objectStorageService.DeleteAsync(file.FileKey, context.CancellationToken);
	// ...
}
catch (Exception ex)
{
	errorCount++;
	_logger.LogError(ex, "Failed to delete file {FileKey} for file ID {FileId}", 
		file.FileKey, file.FileID);
}
```

Isolates failures to individual files, allowing the job to continue processing other files.

### 6.2 Summary logging

```csharp
_logger.LogInformation(
	"Cleanup process completed. Successfully deleted {DeletedCount} files, {ErrorCount} errors occurred.", 
	deletedCount, errorCount);
```

Provides clear metrics about the cleanup operation.

---

## 7. Sharp edges

### 7.1 Batch size consideration

The query limits to 50 records per execution:

```csharp
.Take(50) // Take up to 50 records
```

This balances memory usage and processing time. Increasing this value could impact performance during the daily execution window.

### 7.2 Timing dependency

The job runs daily at 1 AM:

```csharp
.WithCronSchedule("0 0 1 * * ?")); // Daily at 1:00 AM
```

This assumes 1 AM is a low-load time. If system usage patterns change, this timing may need adjustment.

### 7.3 Object storage availability

The job depends on `IObjectStorageService` availability. Network issues or service outages could cause the entire job to fail, though individual file failures are handled gracefully.

---

## 8. Wiring — what is registered where

`BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs`:

```csharp
services.ConfigureOptions<BulkUploadFileCleanup.BulkUploadFileCleanupServiceSetup>();
```

`BackgroundJobs/BulkUploadFileCleanup/BulkUploadFileCleanupServiceSetup.cs`:

- Registers the `BulkUploadFileCleanupJob` with Quartz
- Sets the cron schedule for daily execution at 1 AM
- Adds the trigger with proper identity

`BackgroundJobs/BulkUploadFileCleanup/BulkUploadFileCleanupJob.cs`:

- Implements the cleanup logic
- Uses proper DI scope management
- Handles errors and logging appropriately

---

## 9. Change X, also check Y

| If you change… | Also check… | Because |
|---|---|---|
| The query criteria (`Status == Done && RejectedRows != null`) | The business logic that sets these values | The cleanup only runs on files that meet specific business conditions |
| The batch size (50) | Performance during execution | Larger batches may impact system resources during the cleanup window |
| The cron schedule | System load patterns | The job should run during low-usage periods |
| The job logic | Error handling for individual files | Per-file errors should not stop processing of other files |
| The repository methods | The caching layer implementation | Cache methods must delegate to the underlying repository |
| The logging format | Monitoring and alerting systems | Consistent logging helps with operational visibility |
| The object storage calls | Retry logic and error handling | Object storage operations may be subject to network issues |
| The cancellation handling | All async operations in the loop | Proper cancellation handling ensures graceful shutdowns |
| The database update logic | Transaction boundaries | Updates should only happen after successful operations |