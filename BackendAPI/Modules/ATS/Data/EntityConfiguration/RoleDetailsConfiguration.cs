namespace ATS.Data.EntityConfiguration;

public class RoleDetailsConfiguration : IEntityTypeConfiguration<RoleDetails>
{
	public void Configure(EntityTypeBuilder<RoleDetails> builder)
	{
		builder.ToTable("RoleDetails", "ats");

		builder.HasKey(x => x.RoleId);

		builder.Property(x => x.RoleId)
			.IsRequired()
			.ValueGeneratedOnAdd();

		builder.Property(x => x.RoleName)
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(x => x.RoleDescription)
			.HasMaxLength(500)
			.IsRequired();

		builder.Property(x => x.IsActive)
			.IsRequired();

		builder.Property(x => x.CreatedAt)
			.IsRequired();

		builder.Property(x => x.UpdatedAt)
			.IsRequired();

		builder.HasIndex(x => x.RoleName)
			.IsUnique();
	}
}
