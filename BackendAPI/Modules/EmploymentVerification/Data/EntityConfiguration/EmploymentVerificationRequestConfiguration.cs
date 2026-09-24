namespace EmploymentVerification.Data.EntityConfiguration;

public sealed class EmploymentVerificationRequestConfiguration
	: IEntityTypeConfiguration<EmploymentVerificationRequest>
{
	public void Configure(
		EntityTypeBuilder<EmploymentVerificationRequest> builder)
	{
		builder.ToTable(
			"EmploymentVerificationRequests",
			"employment_verification");

		builder.HasKey(request => request.Id);

		builder.Property(request => request.CandidateName)
			.HasMaxLength(200)
			.IsRequired();

		builder.Property(request => request.PreviousEmployer)
			.HasMaxLength(250)
			.IsRequired();

		builder.Property(request => request.Position)
			.HasMaxLength(200)
			.IsRequired();

		builder.Property(request => request.HrEmail)
			.HasMaxLength(320)
			.IsRequired();

		builder.Property(request => request.VerificationTokenHash)
			.HasMaxLength(128)
			.IsRequired();

		builder.Property(request => request.Status)
			.HasConversion<string>()
			.HasMaxLength(20);

		builder.Property(request => request.RecipientSource)
			.HasMaxLength(30);

		builder.HasIndex(request => request.VerificationTokenHash)
			.IsUnique();

		builder.HasIndex(request => new
		{
			request.Status,
			request.RequestedAt
		});

		// Drives the availability check, which asks "which (order, employer slot)
		// pairs already have a live request". AtsSubjectId alone was never indexed
		// even though that query has always projected and de-duplicated it.
		// Deliberately NOT unique: a lapsed or failed request releases its segment, and
		// the retry is a new row with its own token and expiry rather than an edit of
		// the old one, so a segment legitimately accumulates several rows over time.
		//
		// Duplicate prevention is therefore the availability check in
		// ListBlockedSegmentsAsync, not a constraint. That read-then-insert has no lock
		// across it, so a Quartz misfire or a second writer could in principle slip a
		// duplicate through; a PARTIAL unique index over the blocking statuses only
		// would close it without forbidding legitimate retries, if that ever proves
		// necessary.
		builder.HasIndex(request => new
		{
			request.AtsSubjectId,
			request.EmploymentSegment
		});
	}
}
