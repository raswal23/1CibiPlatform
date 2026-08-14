namespace ATS.BackgroundJobs.EmailNotification;

public class EmailNotificationBackgroundJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(EmailNotificationBackgroundJob));
		options.AddJob<EmailNotificationBackgroundJob>(opts => opts.WithIdentity(jobKey));

		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("EmailNotificationTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInSeconds(10).RepeatForever()));
	}
}