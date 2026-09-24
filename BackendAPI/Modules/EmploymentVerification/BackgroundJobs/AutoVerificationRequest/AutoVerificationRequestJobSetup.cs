namespace EmploymentVerification.BackgroundJobs.AutoVerificationRequest;

public class AutoVerificationRequestJobSetup : IConfigureOptions<QuartzOptions>
{
	public void Configure(QuartzOptions options)
	{
		var jobKey = new JobKey(nameof(AutoVerificationRequestJob));
		options.AddJob<AutoVerificationRequestJob>(opts => opts.WithIdentity(jobKey));

		// Five minutes, where the ATS email job runs every 5 seconds - the trigger here
		// is a candidate submitting an application form, which arrives in minutes, and
		// nothing downstream is waiting on the verification to complete.
		//
		// It is also the blast radius of a failed pass: the eligibility query is derived
		// from current state rather than a claim, so a crashed pass releases nothing and
		// the next one picks up exactly the same segments five minutes later.
		//
		// The scheduler is clustered and the job is [DisallowConcurrentExecution], so
		// the trigger fires on one node only - no distributed lock is needed here.
		options.AddTrigger(opts => opts
			.ForJob(jobKey)
			.WithIdentity("AutoVerificationRequestTrigger")
			.WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever()));
	}
}
