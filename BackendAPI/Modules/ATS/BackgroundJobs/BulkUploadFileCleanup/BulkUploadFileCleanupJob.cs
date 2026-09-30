using ATS.Data.Entities;
using ATS.Data.Repository;
using BuildingBlocks.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ATS.BackgroundJobs.BulkUploadFileCleanup;

/// <summary>
/// Quartz job that cleans up file keys in BulkUploadFileDetails when status is "Done" 
/// and rejected rows is not null. Runs daily at 1 AM.
/// </summary>
[DisallowConcurrentExecution]
public class BulkUploadFileCleanupJob : IJob
{
	private readonly ILogger<BulkUploadFileCleanupJob> _logger;
	private readonly IServiceScopeFactory _scopeFactory;

	public BulkUploadFileCleanupJob(
		ILogger<BulkUploadFileCleanupJob> logger,
		IServiceScopeFactory scopeFactory)
	{
		_logger = logger;
		_scopeFactory = scopeFactory;
	}

	public async Task Execute(IJobExecutionContext context)
	{
		_logger.LogInformation("Starting bulk upload file cleanup process at {Timestamp}.", DateTime.UtcNow);

		using var scope = _scopeFactory.CreateScope();
		var repository = scope.ServiceProvider.GetRequiredService<IATSRepository>();
		var objectStorageService = scope.ServiceProvider.GetRequiredService<IObjectStorageService>();

		try
		{
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

			_logger.LogInformation(
				"Cleanup process completed. Successfully deleted {DeletedCount} files, {ErrorCount} errors occurred.",
				deletedCount, errorCount);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error occurred during bulk upload file cleanup process.");
			throw;
		}
	}
}