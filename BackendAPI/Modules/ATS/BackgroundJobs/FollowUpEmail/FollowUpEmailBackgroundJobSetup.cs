namespace ATS.BackgroundJobs.FollowUpEmail;

public class FollowUpEmailBackgroundJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(FollowUpEmailBackgroundJob));
		options.AddJob<FollowUpEmailBackgroundJob>(opts => opts.WithIdentity(jobKey));

		// Hourly, where the email job runs every 5 seconds - because the unit here is DAYS.
		// PackageDetails.FollowUpEmail is a count of daily reminders, so polling harder would
		// buy nothing but a join against PackageDetails every few seconds.
		//
		// Hourly is also why LastFollowUpSentDate has to exist. A due row stays due for the
		// rest of its local day, so every one of the day's remaining passes would release it
		// again; the date guard collapses those to the first pass after the order's time of
		// day. Deleting the column and keeping this trigger means up to 24 reminders a day.
		//
		// The hour is also the granularity of the send time: a reminder for an 08:00 order
		// goes out on the first pass at or after 08:00, not at 08:00 exactly.
		//
		// An hour is likewise the blast radius if a pass fails: the date stamp is written in
		// the same UPDATE as the release, so a crashed pass releases nothing and the next one
		// picks the same rows up, at most an hour later.
		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("FollowUpEmailTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInHours(1).RepeatForever()));
	}
}
