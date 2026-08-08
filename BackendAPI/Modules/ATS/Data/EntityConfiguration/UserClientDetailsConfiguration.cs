namespace ATS.Data.EntityConfiguration;

public class UserClientDetailsConfiguration : IEntityTypeConfiguration<UserClientDetails>
{
	public void Configure(EntityTypeBuilder<UserClientDetails> builder)
	{
		builder.ToTable("UserClientDetails", "ats");

		builder.HasKey(x => x.UserId);

		builder.Property(x => x.UserId)
			.IsRequired()
			.ValueGeneratedNever();

		builder.Property(x => x.ClientId)
			.IsRequired();

		builder.Property(x => x.CreatedAt)
			.IsRequired();

		builder.Property(x => x.UpdatedAt)
			.IsRequired();

		// ClientDetails is keyed by (ClientId, PackageId), so ClientId alone cannot be
		// an EF foreign key principal. Assignment writes validate the logical client.
		builder.HasIndex(x => x.ClientId);
	}
}
