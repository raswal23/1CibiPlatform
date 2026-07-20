namespace ATS.Data.EntityConfiguration;

public class ApplicantSearchProjectionConfiguration : IEntityTypeConfiguration<ApplicantSearchProjection>
{
	public void Configure(EntityTypeBuilder<ApplicantSearchProjection> builder)
	{
		builder.ToTable("ApplicantSearchProjection", "ats");

		builder.HasKey(x => x.EmailInvitationRequestId);

		builder.Property(x => x.EmailInvitationRequestId)
			.IsRequired()
			.ValueGeneratedNever();

		builder.Property(x => x.FirstName).HasMaxLength(255);
		builder.Property(x => x.LastName).HasMaxLength(255);
		builder.Property(x => x.MiddleInitial).HasMaxLength(255);
		builder.Property(x => x.EmailAddress).HasMaxLength(255);
		builder.Property(x => x.MobileNumber).HasMaxLength(255);
		builder.Property(x => x.SelectPackage).HasMaxLength(255);
		builder.Property(x => x.RushNormal).HasMaxLength(255);
		builder.Property(x => x.OrderStatus).HasMaxLength(255);
		builder.Property(x => x.ApplicationFormStatus).HasMaxLength(255);
		builder.Property(x => x.PositionAppliedFor).HasMaxLength(255);
		builder.Property(x => x.MaritalStatus).HasMaxLength(255);
		builder.Property(x => x.Nationality).HasMaxLength(255);
		builder.Property(x => x.Sex).HasMaxLength(255);
		builder.Property(x => x.SSS).HasMaxLength(255);
		builder.Property(x => x.TIN).HasMaxLength(255);
		builder.Property(x => x.EmailAlternative).HasMaxLength(255);
		builder.Property(x => x.CurrentAddress).HasMaxLength(255);
		builder.Property(x => x.CurrentCity).HasMaxLength(255);
		builder.Property(x => x.CurrentProvince).HasMaxLength(255);
		builder.Property(x => x.CurrentCountry).HasMaxLength(255);
		builder.Property(x => x.CurrentPostalCode).HasMaxLength(255);
		builder.Property(x => x.PermanentAddress).HasMaxLength(255);
		builder.Property(x => x.PermanentCity).HasMaxLength(255);
		builder.Property(x => x.PermanentProvince).HasMaxLength(255);
		builder.Property(x => x.PermanentCountry).HasMaxLength(255);
		builder.Property(x => x.PermanentPostalCode).HasMaxLength(255);
		builder.Property(x => x.HighestEducationalAttainment).HasMaxLength(255);
		builder.Property(x => x.BachelorsSchoolName).HasMaxLength(255);
		builder.Property(x => x.BachelorsDegree).HasMaxLength(255);
		builder.Property(x => x.MastersSchoolName).HasMaxLength(255);
		builder.Property(x => x.MastersDegree).HasMaxLength(255);
		builder.Property(x => x.PhDSchoolName).HasMaxLength(255);
		builder.Property(x => x.DoctorateDegree).HasMaxLength(255);
		builder.Property(x => x.LicenseName).HasMaxLength(255);
		builder.Property(x => x.LicenseNumber).HasMaxLength(255);
		builder.Property(x => x.Emp1CompanyName).HasMaxLength(255);
		builder.Property(x => x.Emp1JobTitle).HasMaxLength(255);
		builder.Property(x => x.Emp2CompanyName).HasMaxLength(255);
		builder.Property(x => x.Emp2JobTitle).HasMaxLength(255);
		builder.Property(x => x.Emp3CompanyName).HasMaxLength(255);
		builder.Property(x => x.Emp3JobTitle).HasMaxLength(255);
		builder.Property(x => x.Ref1FullName).HasMaxLength(255);
		builder.Property(x => x.Ref1ContactNumber).HasMaxLength(255);
		builder.Property(x => x.Ref2FullName).HasMaxLength(255);
		builder.Property(x => x.Ref2ContactNumber).HasMaxLength(255);
		builder.Property(x => x.Ref3FullName).HasMaxLength(255);
		builder.Property(x => x.Ref3ContactNumber).HasMaxLength(255);
		builder.Property(x => x.SignerName).HasMaxLength(255);

		builder.HasOne(x => x.EmailInvitationRequest)
			.WithOne(x => x.ApplicantSearchProjection)
			.HasForeignKey<ApplicantSearchProjection>(x => x.EmailInvitationRequestId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired();

		builder.HasIndex(x => x.EmailAddress);
		builder.HasIndex(x => x.LastName);
		builder.HasIndex(x => x.OrderStatus);
	}
}
