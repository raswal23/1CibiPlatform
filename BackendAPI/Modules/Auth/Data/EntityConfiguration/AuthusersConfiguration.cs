namespace Auth.Data.EntityConfiguration;

public class AuthusersConfiguration : IEntityTypeConfiguration<Authusers>
{
	public void Configure(EntityTypeBuilder<Authusers> builder)
	{

		builder.HasKey(u => u.Id);

		builder.Property(e => e.Id)
			.IsRequired();

		builder.Property(u => u.Id)
			   .ValueGeneratedNever();

		builder.Property(u => u.Email)
			   .IsRequired()
			   .HasMaxLength(255);

		builder.Property(u => u.PasswordHash)
			   .IsRequired();

		builder.Property(u => u.FirstName)
			   .IsRequired()
			   .HasMaxLength(100);

		builder.Property(u => u.LastName)
			   .IsRequired()
			   .HasMaxLength(100);

		builder.Property(u => u.MiddleName)
			   .HasMaxLength(100);

		builder.Property(u => u.IsActive)
			   .HasDefaultValue(true);

		builder.Property(u => u.IsApproved)
			   .HasDefaultValue(false);

		builder.Property(u => u.CreatedAt)
			   .IsRequired()
			   .HasDefaultValueSql("timezone('utc', now())");

		// Matches the keyset pagination ordering of the ATS user directory.
		builder.HasIndex(u => new { u.LastName, u.FirstName, u.Id });
	}
}
