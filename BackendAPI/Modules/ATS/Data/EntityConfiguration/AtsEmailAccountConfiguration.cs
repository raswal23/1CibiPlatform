namespace ATS.Data.EntityConfiguration;

public class AtsEmailAccountConfiguration : IEntityTypeConfiguration<AtsEmailAccount>
{
	public void Configure(EntityTypeBuilder<AtsEmailAccount> builder)
	{
		builder.ToTable("EmailAccounts", "ats");

		builder.HasKey(x => x.AtsEmailAccountId);

		builder.Property(x => x.AtsEmailAccountId)
			   .ValueGeneratedOnAdd();

		builder.Property(x => x.DisplayName)
			   .HasMaxLength(120)
			   .IsRequired();

		builder.Property(x => x.EmailAddress)
			   .HasMaxLength(255)
			   .IsRequired();

		builder.Property(x => x.SmtpHost)
			   .HasMaxLength(255)
			   .IsRequired();

		builder.Property(x => x.SmtpPort)
			   .IsRequired();

		// Base64url of nonce + ciphertext + tag with a version prefix, so it is several times
		// the length of the password it protects.
		builder.Property(x => x.EncryptedPassword)
			   .HasMaxLength(512)
			   .IsRequired();

		builder.Property(x => x.Priority)
			   .IsRequired();

		builder.Property(x => x.IsActive)
			   .IsRequired();

		builder.Property(x => x.DailySendLimit)
			   .IsRequired();

		builder.Property(x => x.VerificationStatus)
			   .HasMaxLength(40)
			   .IsRequired();

		builder.Property(x => x.ConsecutiveFailureCount)
			   .IsRequired();

		// Holds a classified SMTP response, which can carry a provider's full explanatory
		// sentence and a help URL.
		builder.Property(x => x.LastFailureReason)
			   .HasMaxLength(500);

		builder.Property(x => x.CreatedAt)
			   .IsRequired();

		builder.Property(x => x.UpdatedAt)
			   .IsRequired();

		// Two rows for one mailbox would share a provider quota the selector counts separately,
		// so it would believe it has twice the headroom it does.
		builder.HasIndex(x => x.EmailAddress)
			   .IsUnique();

		// Unique so "the next account" is never ambiguous. A shared priority would make
		// failover depend on row order, which is neither stable nor testable.
		builder.HasIndex(x => x.Priority)
			   .IsUnique();

		// The selector reads this table on the send hot path and orders by priority within the
		// sendable rows, so the filter columns lead and the ordering column follows.
		builder.HasIndex(x => new { x.IsActive, x.VerificationStatus, x.Priority });
	}
}
