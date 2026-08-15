namespace ATS.BackgroundJobs.EmailNotificationRecovery;

public class EmailNotificationRecoveryJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(EmailNotificationRecoveryJob));
		options.AddJob<EmailNotificationRecoveryJob>(opts => opts.WithIdentity(jobKey));

		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("EmailNotificationRecoveryTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInMinutes(30).RepeatForever()));
	}
}
