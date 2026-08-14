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

		builder.HasIndex(request => request.VerificationTokenHash)
			.IsUnique();

		builder.HasIndex(request => new
		{
			request.Status,
			request.RequestedAt
		});
	}
}
