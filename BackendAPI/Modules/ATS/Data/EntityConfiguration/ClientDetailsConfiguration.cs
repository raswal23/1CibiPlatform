namespace ATS.Data.EntityConfiguration;

public class ClientDetailsConfiguration : IEntityTypeConfiguration<ClientDetails>
{
	public void Configure(EntityTypeBuilder<ClientDetails> builder)
	{
		builder.ToTable("ClientDetails", "ats");

		builder.HasKey(x => x.ClientId);

		builder.Property(x => x.ClientId)
			.IsRequired()
			.ValueGeneratedNever();

		builder.Property(x => x.ClientName)
			.HasMaxLength(255)
			.IsRequired();

		builder.Property(x => x.IsActive)
			.IsRequired();

		builder.Property(x => x.CreatedAt)
			.IsRequired();

		builder.HasIndex(x => x.ClientName)
			.IsUnique();
	}
}
