namespace ATS.Data.EntityConfiguration;

public class ClientDetailsConfiguration : IEntityTypeConfiguration<ClientDetails>
{
	public void Configure(EntityTypeBuilder<ClientDetails> builder)
	{
		builder.ToTable("ClientDetails", "ats");

		builder.HasKey(x => new { x.ClientId, x.PackageId });

		builder.Property(x => x.ClientId)
			.IsRequired()
			.ValueGeneratedOnAdd();

		builder.Property(x => x.ClientName)
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(x => x.ClientDescription)
			.HasMaxLength(500)
			.IsRequired();

		builder.Property(x => x.IsActive)
			.IsRequired();

		builder.Property(x => x.CreatedAt)
			.IsRequired();

		builder.Property(x => x.UpdatedAt)
			.IsRequired();

		builder.HasOne(x => x.Package)
			.WithMany()
			.HasForeignKey(x => x.PackageId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => x.ClientName);
	}
}
