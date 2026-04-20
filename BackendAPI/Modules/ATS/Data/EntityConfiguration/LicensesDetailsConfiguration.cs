namespace ATS.Data.EntityConfiguration;

public class LicensesDetailsConfiguration : IEntityTypeConfiguration<LicensesDetails>
{
    public void Configure(EntityTypeBuilder<LicensesDetails> builder)
    {
        builder.ToTable("LicensesDetails", "ats");

        builder.HasKey(l => l.LicensesDetailsID);

        builder.Property(l => l.LicensesDetailsID)
               .IsRequired()
               .ValueGeneratedNever();

        builder.Property(l => l.EmailInvitationID)
               .IsRequired();

        builder.Property(l => l.LicenseName).HasMaxLength(100);
        builder.Property(l => l.LicenseNumber).HasMaxLength(100);
        builder.Property(l => l.LicenseExpiryDate).HasMaxLength(100);

        builder.Property(l => l.LicenseUpload)
               .IsRequired(false);

        builder.Property(l => l.CreatedDate)
               .IsRequired(false);

        // Relationship to EmailInvitationRequest
        builder.HasOne<EmailInvitationRequest>()
               .WithOne(e => e.LicensesDetails)
               .HasForeignKey<LicensesDetails>(l => l.EmailInvitationID)
               .OnDelete(DeleteBehavior.Cascade)
               .IsRequired();
    }
}
