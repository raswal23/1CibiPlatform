namespace ATS.Data.EntityConfiguration;

public class ProfessionalExperiencesConfiguration : IEntityTypeConfiguration<ProfessionalExperiences>
{
    public void Configure(EntityTypeBuilder<ProfessionalExperiences> builder)
    {
        builder.ToTable("ProfessionalExperiences", "ats");

        builder.HasKey(p => p.ProfessionalExperiencesID);

        builder.Property(p => p.ProfessionalExperiencesID)
               .IsRequired()
               .ValueGeneratedNever();

        builder.Property(p => p.EmailInvitationID)
               .IsRequired();

        // Emp1
        builder.Property(p => p.Emp1CompanyName).HasMaxLength(100);
        builder.Property(p => p.Emp1CurrentlyEmployed).HasMaxLength(100);
        builder.Property(p => p.Emp1PermissionToContact).HasMaxLength(100);
        builder.Property(p => p.Emp1CompanyAddress).HasMaxLength(100);
        builder.Property(p => p.Emp1StartDate).HasMaxLength(100);
        builder.Property(p => p.Emp1EndDate).HasMaxLength(100);
        builder.Property(p => p.Emp1JobTitle).HasMaxLength(100);
        builder.Property(p => p.Emp1ReasonForLeaving).HasMaxLength(100);
        builder.Property(p => p.Emp1SupervisorName).HasMaxLength(100);
        builder.Property(p => p.Emp1SupervisorContactNumber).HasMaxLength(100);
        builder.Property(p => p.Emp1SupervisorEmail).HasMaxLength(100);

        // Emp2
        builder.Property(p => p.Emp2CompanyName).HasMaxLength(100);
        builder.Property(p => p.Emp2CurrentlyEmployed).HasMaxLength(100);
        builder.Property(p => p.Emp2PermissionToContact).HasMaxLength(100);
        builder.Property(p => p.Emp2CompanyAddress).HasMaxLength(100);
        builder.Property(p => p.Emp2StartDate).HasMaxLength(100);
        builder.Property(p => p.Emp2EndDate).HasMaxLength(100);
        builder.Property(p => p.Emp2JobTitle).HasMaxLength(100);
        builder.Property(p => p.Emp2ReasonForLeaving).HasMaxLength(100);
        builder.Property(p => p.Emp2SupervisorName).HasMaxLength(100);
        builder.Property(p => p.Emp2SupervisorContactNumber).HasMaxLength(100);
        builder.Property(p => p.Emp2SupervisorEmail).HasMaxLength(100);

        // Emp3
        builder.Property(p => p.Emp3CompanyName).HasMaxLength(100);
        builder.Property(p => p.Emp3CurrentlyEmployed).HasMaxLength(100);
        builder.Property(p => p.Emp3PermissionToContact).HasMaxLength(100);
        builder.Property(p => p.Emp3CompanyAddress).HasMaxLength(100);
        builder.Property(p => p.Emp3StartDate).HasMaxLength(100);
        builder.Property(p => p.Emp3EndDate).HasMaxLength(100);
        builder.Property(p => p.Emp3JobTitle).HasMaxLength(100);
        builder.Property(p => p.Emp3ReasonForLeaving).HasMaxLength(100);
        builder.Property(p => p.Emp3SupervisorName).HasMaxLength(100);
        builder.Property(p => p.Emp3SupervisorContactNumber).HasMaxLength(100);
        builder.Property(p => p.Emp3SupervisorEmail).HasMaxLength(100);

        builder.Property(p => p.COEUpload).IsRequired(false);
        builder.Property(p => p.CreatedDate).IsRequired(false);

        // Relationship to EmailInvitationRequest
        builder.HasOne<EmailInvitationRequest>()
               .WithOne(e => e.ProfessionalExperiences)
               .HasForeignKey<ProfessionalExperiences>(pe => pe.EmailInvitationID)
               .OnDelete(DeleteBehavior.Cascade)
               .IsRequired();
    }
}
