namespace ATS.Data.EntityConfiguration;

public class PackageDetailsConfiguration : IEntityTypeConfiguration<PackageDetails>
{
	public void Configure(EntityTypeBuilder<PackageDetails> builder)
	{
		builder.ToTable("PackageDetails", "ats");

		builder.HasKey(x => x.PackageId);

		builder.Property(x => x.PackageId)
			.IsRequired()
			.ValueGeneratedOnAdd();

		builder.Property(x => x.PackageName)
			.HasMaxLength(255)
			.IsRequired();

		builder.Property(x => x.PackageDescription)
			.HasMaxLength(500)
			.IsRequired();

		builder.Property(x => x.IsActive)
			.IsRequired();

		builder.Property(x => x.FollowUpEmail)
			.IsRequired();

		// Nullable on purpose: null means the screening type was never chosen,
		// which the UI shows as "Not set" rather than defaulting to Data.
		builder.Property(x => x.AutoChasing);

		builder.Property(x => x.CreatedAt)
			.IsRequired();

		builder.Property(x => x.UpdatedAt)
			.IsRequired();

		builder.HasIndex(x => x.PackageName)
			.IsUnique();
	}
}
