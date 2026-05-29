namespace ATS.Data.EntityConfiguration;

public class PersonalDetailsConfiguration : IEntityTypeConfiguration<PersonalDetails>
{
    public void Configure(EntityTypeBuilder<PersonalDetails> builder)
    {
        builder.ToTable("PersonalDetails", "ats");

        builder.HasKey(p => p.PersonalID);

        builder.Property(p => p.PersonalID)
               .IsRequired()
               .ValueGeneratedNever();

        builder.Property(p => p.EmailInvitationID)
               .IsRequired();

        builder.Property(p => p.FirstName)
               .HasMaxLength(100);

        builder.Property(p => p.MiddleName)
               .HasMaxLength(100);

        builder.Property(p => p.LastName)
               .HasMaxLength(100);

        builder.Property(p => p.Suffix)
               .HasMaxLength(100);

        builder.Property(p => p.MaritalStatus)
               .HasMaxLength(100);

        builder.Property(p => p.Nationality)
               .HasMaxLength(100);

        builder.Property(p => p.Sex)
               .HasMaxLength(100);

        builder.Property(p => p.SSS)
               .HasMaxLength(100);

        builder.Property(p => p.TIN)
               .HasMaxLength(100);

        builder.Property(p => p.MobileNumber)
               .HasMaxLength(100);

        builder.Property(p => p.TelephoneNumber)
               .HasMaxLength(100);

        builder.Property(p => p.EmailAddress)
               .HasMaxLength(100);

        builder.Property(p => p.EmailAlternative)
               .HasMaxLength(100);

        builder.Property(p => p.AdditionalGovtIDFileKey)
               .HasMaxLength(100);

        builder.Property(p => p.NBIClearanceFileKey)
               .HasMaxLength(100);

        builder.Property(p => p.ResumeFileKey)
               .HasMaxLength(100);

        builder.Property(p => p.CreatedDate)
               .IsRequired(true);

        // Relationship to EmailInvitationRequest
        builder.HasOne<EmailInvitationRequest>()
               .WithOne(e => e.PersonalDetails)
               .HasForeignKey<PersonalDetails>(p => p.EmailInvitationID)
               .OnDelete(DeleteBehavior.Cascade)
               .IsRequired();
    }
}
