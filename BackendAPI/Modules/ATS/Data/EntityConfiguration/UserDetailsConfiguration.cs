namespace ATS.Data.EntityConfiguration;

public class UserDetailsConfiguration : IEntityTypeConfiguration<UserDetails>
{
	public void Configure(EntityTypeBuilder<UserDetails> builder)
	{
		builder.ToTable("UserDetails", "ats");

		builder.HasKey(x => new { x.UserId, x.ModuleId });

		builder.Property(x => x.UserId)
			.IsRequired()
			.ValueGeneratedNever();

		builder.Property(x => x.UserName)
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(x => x.UserEmail)
			.HasMaxLength(256)
			.IsRequired();

		builder.Property(x => x.IsActive)
			.IsRequired();

		builder.Property(x => x.ClientId)
			.IsRequired(false);

		builder.Property(x => x.Site)
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(x => x.RoleId)
			.IsRequired();

		builder.Property(x => x.ModuleId)
			.IsRequired();

		builder.Property(x => x.CreatedAt)
			.IsRequired();

		builder.Property(x => x.UpdatedAt)
			.IsRequired();

		builder.HasOne(x => x.Role)
			.WithMany()
			.HasForeignKey(x => x.RoleId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.Module)
			.WithMany()
			.HasForeignKey(x => x.ModuleId)
			.OnDelete(DeleteBehavior.Restrict);

		// ClientDetails is keyed by (ClientId, PackageId), so ClientId alone cannot be
		// an EF foreign key principal. User writes validate the logical client instead.

		builder.HasIndex(x => x.UserEmail);
		builder.HasIndex(x => x.ClientId);
	}
}
