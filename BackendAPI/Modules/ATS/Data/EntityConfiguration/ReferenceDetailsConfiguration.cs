namespace ATS.Data.EntityConfiguration;

public class ReferenceDetailsConfiguration : IEntityTypeConfiguration<ReferenceDetails>
{
    public void Configure(EntityTypeBuilder<ReferenceDetails> builder)
    {
        builder.ToTable("ReferenceDetails", "ats");

        builder.HasKey(r => r.ReferenceDetailsID);

        builder.Property(r => r.ReferenceDetailsID)
               .IsRequired()
               .ValueGeneratedNever();

        builder.Property(r => r.EmailInvitationID)
               .IsRequired();

        builder.Property(r => r.Ref1FullName).HasMaxLength(100);
        builder.Property(r => r.Ref1ProfessionalRelationship).HasMaxLength(100);
        builder.Property(r => r.Ref1AffiliatedCompany).HasMaxLength(100);
        builder.Property(r => r.Ref1Email).HasMaxLength(100);
        builder.Property(r => r.Ref1ContactNumber).HasMaxLength(100);
        builder.Property(r => r.Ref1ModeOfContact).HasMaxLength(100);
        builder.Property(r => r.Ref1BestTimeToContact).HasMaxLength(100);

        builder.Property(r => r.Ref2FullName).HasMaxLength(100);
        builder.Property(r => r.Ref2ProfessionalRelationship).HasMaxLength(100);
        builder.Property(r => r.Ref2AffiliatedCompany).HasMaxLength(100);
        builder.Property(r => r.Ref2Email).HasMaxLength(100);
        builder.Property(r => r.Ref2ContactNumber).HasMaxLength(100);
        builder.Property(r => r.Ref2ModeOfContact).HasMaxLength(100);
        builder.Property(r => r.Ref2BestTimeToContact).HasMaxLength(100);

        builder.Property(r => r.Ref3FullName).HasMaxLength(100);
        builder.Property(r => r.Ref3ProfessionalRelationship).HasMaxLength(100);
        builder.Property(r => r.Ref3AffiliatedCompany).HasMaxLength(100);
        builder.Property(r => r.Ref3Email).HasMaxLength(100);
        builder.Property(r => r.Ref3ContactNumber).HasMaxLength(100);
        builder.Property(r => r.Ref3ModeOfContact).HasMaxLength(100);
        builder.Property(r => r.Ref3BestTimeToContact).HasMaxLength(100);

        builder.Property(r => r.CreatedDate).IsRequired(true);

        // Relationship to EmailInvitationRequest
        builder.HasOne<EmailInvitationRequest>()
               .WithOne(e => e.ReferenceDetails)
               .HasForeignKey<ReferenceDetails>(rd => rd.EmailInvitationID)
               .OnDelete(DeleteBehavior.Cascade)
               .IsRequired();
    }
}
