namespace ATS.Data.EntityConfiguration;

public class AtsEmailAccountOtpConfiguration : IEntityTypeConfiguration<AtsEmailAccountOtp>
{
	public void Configure(EntityTypeBuilder<AtsEmailAccountOtp> builder)
	{
		builder.ToTable("EmailAccountOtp", "ats");

		builder.HasKey(x => x.AtsEmailAccountOtpId);

		builder.Property(x => x.AtsEmailAccountOtpId)
			   .ValueGeneratedOnAdd();

		builder.Property(x => x.AtsEmailAccountId)
			   .IsRequired();

		builder.Property(x => x.Purpose)
			   .HasMaxLength(20)
			   .IsRequired();

		// Sized for the SHA-512 hex that IHashService produces, with room for a wider
		// algorithm later.
		builder.Property(x => x.OtpCodeHash)
			   .HasMaxLength(256)
			   .IsRequired();

		builder.Property(x => x.AttemptCount)
			   .IsRequired();

		builder.Property(x => x.IsUsed)
			   .IsRequired();

		// A small JSON object: the four credential fields, with the password already
		// protected. Capped rather than unbounded because nothing legitimate reaches it.
		builder.Property(x => x.PendingChangesJson)
			   .HasMaxLength(1_000);

		builder.Property(x => x.CreatedAt)
			   .IsRequired();

		builder.Property(x => x.ExpiresAt)
			   .IsRequired();

		// Cascade: a code for an account that no longer exists can never be verified, and
		// leaving it behind only keeps a hash alive with nothing to match it against.
		builder.HasOne<AtsEmailAccount>()
			   .WithMany()
			   .HasForeignKey(x => x.AtsEmailAccountId)
			   .OnDelete(DeleteBehavior.Cascade);

		// Verification looks up the newest unused code for one account and purpose, so the
		// three filter columns lead and CreatedAt orders within them.
		builder.HasIndex(x => new { x.AtsEmailAccountId, x.Purpose, x.IsUsed, x.CreatedAt })
			   .IsDescending(false, false, false, true);
	}
}
