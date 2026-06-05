namespace ATS.Data.EntityConfiguration;

public class SignatureDetailsConfiguration : IEntityTypeConfiguration<SignatureDetails>
{
	public void Configure(EntityTypeBuilder<SignatureDetails> builder)
	{
		builder.ToTable("SignatureDetails", "ats");

		builder.HasKey(p => p.SignatureDetailsID);

		builder.Property(p => p.SignatureDetailsID)
			   .IsRequired()
			   .ValueGeneratedNever();

		builder.Property(p => p.EmailInvitationID)
			   .IsRequired();

		builder.Property(p => p.SignerName)
			   .HasMaxLength(255);

		builder.Property(e => e.SignatureDate)
			   .HasColumnType("date");

		builder.Property(p => p.SignatureFileKey)
			   .HasMaxLength(255);

		// Relationship to EmailInvitationRequest
		builder.HasOne<EmailInvitationRequest>()
			   .WithOne(e => e.SignatureDetails)
			   .HasForeignKey<SignatureDetails>(p => p.EmailInvitationID)
			   .OnDelete(DeleteBehavior.Cascade)
			   .IsRequired();
	}
}
