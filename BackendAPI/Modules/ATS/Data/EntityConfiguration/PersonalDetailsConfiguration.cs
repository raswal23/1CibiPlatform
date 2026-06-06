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
               .HasMaxLength(255);

        builder.Property(p => p.MiddleName)
               .HasMaxLength(255);

        builder.Property(p => p.LastName)
               .HasMaxLength(255);

        builder.Property(p => p.Suffix)
               .HasMaxLength(255);

        builder.Property(p => p.MaritalStatus)
               .HasMaxLength(255);

        builder.Property(p => p.Nationality)
               .HasMaxLength(255);

        builder.Property(p => p.Sex)
               .HasMaxLength(255);

        builder.Property(p => p.SSS)
               .HasMaxLength(255);

        builder.Property(p => p.TIN)
               .HasMaxLength(255);

        builder.Property(p => p.MobileNumber)
               .HasMaxLength(255);

        builder.Property(p => p.TelephoneNumber)
               .HasMaxLength(255);

        builder.Property(p => p.EmailAddress)
               .HasMaxLength(255);

        builder.Property(p => p.EmailAlternative)
               .HasMaxLength(255);

        builder.Property(p => p.AdditionalGovtIDFileKey)
               .HasMaxLength(255);

        builder.Property(p => p.NBIClearanceFileKey)
               .HasMaxLength(255);

        builder.Property(p => p.ResumeFileKey)
               .HasMaxLength(255);

		builder.Property(p => p.PhilSysImageKey) 
			   .HasMaxLength(255);

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
