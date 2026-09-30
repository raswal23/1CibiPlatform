using Microsoft.Extensions.Options;
using Quartz;

namespace ATS.BackgroundJobs.BulkUploadFileCleanup;

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