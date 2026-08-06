namespace ATS.Data.EntityConfiguration;

public class PackageDetailsConfiguration : IEntityTypeConfiguration<PackageDetails>
{
	public void Configure(EntityTypeBuilder<PackageDetails> builder)
	{
		builder.ToTable("PackageDetails", "ats");

		builder.HasKey(x => x.PackageId);

		builder.Property(x => x.PackageId)
			.IsRequired()
			.ValueGeneratedNever();

		builder.Property(x => x.PackageName)
			.HasMaxLength(255)
			.IsRequired();

		builder.Property(x => x.IsActive)
			.IsRequired();

		builder.Property(x => x.CreatedAt)
			.IsRequired();

		builder.HasIndex(x => x.PackageName)
			.IsUnique();
	}
}
