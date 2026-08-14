namespace ATS.BackgroundJobs.BulkSubmission;

public class BulkSubmissionBackgroundJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(BulkSubmissionBackgroundJob));
		options.AddJob<BulkSubmissionBackgroundJob>(opts => opts.WithIdentity(jobKey));

		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("BulkSubmissionTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInSeconds(10).RepeatForever()));
	}
}
