namespace ATS.Data.EntityConfiguration;

public class AddressDetailsConfiguration : IEntityTypeConfiguration<AddressDetails>
{
    public void Configure(EntityTypeBuilder<AddressDetails> builder)
    {
        builder.ToTable("AddressDetails", "ats");

        builder.HasKey(a => a.AddressId);

        builder.Property(a => a.AddressId)
               .IsRequired()
               .ValueGeneratedNever();

        builder.Property(a => a.EmailInvitationID)
               .IsRequired();

        builder.Property(a => a.CurrentTypeOfOwnership)
               .HasMaxLength(255);

        builder.Property(a => a.CurrentCity)
               .HasMaxLength(255);

        builder.Property(a => a.CurrentProvince)
               .HasMaxLength(255);

        builder.Property(a => a.CurrentCountry)
               .HasMaxLength(255);

        builder.Property(a => a.CurrentAddress)
               .HasMaxLength(255);

        builder.Property(a => a.CurrentPostalCode)
               .HasMaxLength(255);

        builder.Property(a => a.CurrentStayFrom)
               .HasMaxLength(255);

        builder.Property(a => a.PermanentTypeOfOwnership)
               .HasMaxLength(255);

        builder.Property(a => a.PermanentAddress)
               .HasMaxLength(255);

        builder.Property(a => a.PermanentCity)
               .HasMaxLength(255);

        builder.Property(a => a.PermanentProvince)
               .HasMaxLength(255);

        builder.Property(a => a.PermanentCountry)
               .HasMaxLength(255);

        builder.Property(a => a.PermanentPostalCode)
               .HasMaxLength(255);

        builder.Property(a => a.CreatedDate)
               .IsRequired(true);

        builder.HasOne<EmailInvitationRequest>()
               .WithOne(e => e.AddressDetails)
               .HasForeignKey<AddressDetails>(a => a.EmailInvitationID)
               .OnDelete(DeleteBehavior.Cascade)
               .IsRequired();
    }
}
