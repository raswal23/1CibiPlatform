namespace ATS.BackgroundJobs.ApplicantSearchProjection;

public class ApplicantSearchProjectionJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(ApplicantSearchProjectionJob));
		options.AddJob<ApplicantSearchProjectionJob>(opts => opts.WithIdentity(jobKey));

		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("ApplicantSearchProjectionTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInMinutes(1).RepeatForever()));
	}
}
