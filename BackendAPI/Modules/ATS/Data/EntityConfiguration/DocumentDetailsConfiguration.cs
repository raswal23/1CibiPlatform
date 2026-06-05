namespace ATS.Data.EntityConfiguration;

public class DocumentDetailsConfiguration : IEntityTypeConfiguration<DocumentDetails>
{
    public void Configure(EntityTypeBuilder<DocumentDetails> builder)
    {
        builder.ToTable("DocumentDetails", "ats");

        builder.HasKey(d => d.DocumentDetailsID);

        builder.Property(d => d.DocumentDetailsID)
               .IsRequired()
               .ValueGeneratedNever();

        builder.Property(d => d.EmailInvitationID)
               .IsRequired();

        builder.Property(d => d.DocumentName).HasMaxLength(255);
        builder.Property(d => d.DocumentValue).HasMaxLength(255);
        builder.Property(d => d.CreatedDate).IsRequired(false);

        // Relationship to EmailInvitationRequest
        builder.HasOne<EmailInvitationRequest>()
               .WithMany(e => e.Documents)
               .HasForeignKey(d => d.EmailInvitationID)
               .OnDelete(DeleteBehavior.Cascade)
               .IsRequired();
    }
}
