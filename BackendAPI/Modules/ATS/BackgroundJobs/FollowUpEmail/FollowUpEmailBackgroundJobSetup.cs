namespace ATS.BackgroundJobs.FollowUpEmail;

public class FollowUpEmailBackgroundJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(FollowUpEmailBackgroundJob));
		options.AddJob<FollowUpEmailBackgroundJob>(opts => opts.WithIdentity(jobKey));

		// Hourly, where the email job runs every 5 seconds - because the unit here is DAYS.
		// PackageDetails.FollowUpEmail is a day count, so the finest resolution that means
		// anything to an operator is "some time on the day it comes due"; polling harder
		// would buy nothing but a join against PackageDetails every few seconds.
		//
		// An hour is also the blast radius if a pass fails: the fire-once stamp is written in
		// the same UPDATE as the release, so a crashed pass releases nothing and the next one
		// picks the same rows up, at most an hour later.
		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("FollowUpEmailTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInHours(1).RepeatForever()));
	}
}
