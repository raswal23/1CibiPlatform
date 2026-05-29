namespace ATS.Data.EntityConfiguration;

public class AddressDetailsConfiguration : IEntityTypeConfiguration<AddressDetails>
{
    public void Configure(EntityTypeBuilder<AddressDetails> builder)
    {
        builder.ToTable("AddressDetails", "ats");

        builder.HasKey(a => a.Address);

        builder.Property(a => a.Address)
               .IsRequired()
               .ValueGeneratedNever();

        builder.Property(a => a.EmailInvitationID)
               .IsRequired();

        builder.Property(a => a.CurrentTypeOfOwnership)
               .HasMaxLength(100);

        builder.Property(a => a.CurrentCity)
               .HasMaxLength(100);

        builder.Property(a => a.CurrentProvince)
               .HasMaxLength(100);

        builder.Property(a => a.CurrentCountry)
               .HasMaxLength(100);

        builder.Property(a => a.CurrentAddress)
               .HasMaxLength(100);

        builder.Property(a => a.CurrentPostalCode)
               .HasMaxLength(100);

        builder.Property(a => a.CurrentStayFrom)
               .HasMaxLength(100);

        builder.Property(a => a.PermanentTypeOfOwnership)
               .HasMaxLength(100);

        builder.Property(a => a.PermanentAddress)
               .HasMaxLength(100);

        builder.Property(a => a.PermanentCity)
               .HasMaxLength(100);

        builder.Property(a => a.PermanentProvince)
               .HasMaxLength(100);

        builder.Property(a => a.PermanentCountry)
               .HasMaxLength(100);

        builder.Property(a => a.PermanentPostalCode)
               .HasMaxLength(100);

        builder.Property(a => a.CreatedDate)
               .IsRequired(false);

        builder.HasOne<EmailInvitationRequest>()
               .WithOne(e => e.AddressDetails)
               .HasForeignKey<AddressDetails>(a => a.EmailInvitationID)
               .OnDelete(DeleteBehavior.Cascade)
               .IsRequired();
    }
}
