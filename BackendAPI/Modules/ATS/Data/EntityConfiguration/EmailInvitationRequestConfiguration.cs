namespace ATS.Data.EntityConfiguration;

public class EmailInvitationRequestConfiguration : IEntityTypeConfiguration<EmailInvitationRequest>
{
    public void Configure(EntityTypeBuilder<EmailInvitationRequest> builder)
    {
        builder.ToTable("EmailInvitationRequest", "ats");

        builder.HasKey(e => e.EmailInvitationID);

        builder.Property(e => e.EmailInvitationID)
               .IsRequired()
               .ValueGeneratedNever();

        builder.Property(e => e.LastName)
               .HasMaxLength(100);

        builder.Property(e => e.FirstName)
               .HasMaxLength(100);

        builder.Property(e => e.MiddleInitial)
               .HasMaxLength(100);

        builder.Property(e => e.EmailAddress)
               .HasMaxLength(100);

        builder.Property(e => e.MobileNumber)
               .HasMaxLength(100);

        builder.Property(e => e.SelectPackage)
               .HasMaxLength(100);

        builder.Property(e => e.RushNormal)
               .HasMaxLength(100);

        builder.Property(e => e.HashToken)
               .HasMaxLength(100);

        builder.Property(e => e.HashTokenCreated)
               .IsRequired(true);

        builder.Property(e => e.HashTokenExpiration)
               .IsRequired(true);
    }
}
