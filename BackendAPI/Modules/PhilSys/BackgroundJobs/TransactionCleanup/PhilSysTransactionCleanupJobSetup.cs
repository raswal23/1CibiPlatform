namespace PhilSys.BackgroundJobs.TransactionCleanup;

public class PhilSysTransactionCleanupJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(PhilSysTransactionCleanupJob));
		options.AddJob<PhilSysTransactionCleanupJob>(opts => opts.WithIdentity(jobKey));

		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("PhilSysTransactionCleanupTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInHours(12).RepeatForever()));
	}
}
