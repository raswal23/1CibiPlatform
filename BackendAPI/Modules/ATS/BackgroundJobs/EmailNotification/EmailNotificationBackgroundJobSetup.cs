namespace ATS.BackgroundJobs.EmailNotification;

public class EmailNotificationBackgroundJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(EmailNotificationBackgroundJob));
		options.AddJob<EmailNotificationBackgroundJob>(opts => opts.WithIdentity(jobKey));

		// 5 seconds, not 1. The job is [DisallowConcurrentExecution], so a trigger that
		// fires while a pass is running is skipped - the interval only decides how quickly
		// an IDLE worker notices new work. At 1s that was two queries a second forever
		// (the stale-claim UPDATE and the claiming CTE) to shave at most four seconds off
		// a delay no candidate can perceive.
		//
		// It is also not the send rate. Throughput is bounded by AtsEmailDeliveryOptions
		// (MaxSendsPerSecond), so shortening this interval polls the database harder
		// without sending a single message faster.
		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("EmailNotificationTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInSeconds(5).RepeatForever()));
	}
}