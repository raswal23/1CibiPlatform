namespace ATS.BackgroundJobs.OMSTicketing;

public class OMSTicketingBackgroundJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(OMSTicketingBackgroundJob));
		options.AddJob<OMSTicketingBackgroundJob>(opts => opts.WithIdentity(jobKey));

		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("OMSTicketingTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInSeconds(10).RepeatForever()));
	}
}
