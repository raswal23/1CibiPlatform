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
               .HasMaxLength(255);

        builder.Property(e => e.FirstName)
               .HasMaxLength(255);

        builder.Property(e => e.MiddleInitial)
               .HasMaxLength(255);

        builder.Property(e => e.EmailAddress)
               .HasMaxLength(255);

        builder.Property(e => e.MobileNumber)
               .HasMaxLength(255);

        builder.Property(e => e.SelectPackage)
               .HasMaxLength(255);

        builder.Property(e => e.RushNormal)
               .HasMaxLength(255);

        builder.Property(e => e.HashToken)
               .HasMaxLength(255);

        builder.Property(e => e.HashTokenCreated)
               .IsRequired(true);

        builder.Property(e => e.HashTokenExpiration)
               .IsRequired(true);

		builder.Property(pt => pt.Status)
			   .HasMaxLength(255);
	}
}
