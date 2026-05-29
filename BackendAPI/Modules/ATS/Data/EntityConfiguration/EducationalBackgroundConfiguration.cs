namespace ATS.Data.EntityConfiguration;

public class EducationalBackgroundConfiguration : IEntityTypeConfiguration<EducationalBackground>
{
    public void Configure(EntityTypeBuilder<EducationalBackground> builder)
    {
        builder.ToTable("EducationalBackground", "ats");

        builder.HasKey(e => e.EducationalBackgroundID);

        builder.Property(e => e.EducationalBackgroundID)
               .IsRequired()
               .ValueGeneratedNever();

        builder.Property(e => e.EmailInvitationID)
               .IsRequired();

        builder.Property(e => e.HighestEducationalAttainment).HasMaxLength(100);
        builder.Property(e => e.HighSchoolName).HasMaxLength(100);
        builder.Property(e => e.HighSchoolAddress).HasMaxLength(100);
        builder.Property(e => e.HighSchoolGraduationDate).HasColumnType("date");
        builder.Property(e => e.HighSchoolDiplomaFileKey).HasMaxLength(100);
        builder.Property(e => e.SeniorHighSchoolName).HasMaxLength(100);
        builder.Property(e => e.SeniorHighSchoolAddress).HasMaxLength(100);
        builder.Property(e => e.SeniorHighSchoolGraduationDate).HasColumnType("date");
        builder.Property(e => e.SeniorHighSchoolDiplomaFileKey).HasMaxLength(100);
        builder.Property(e => e.CollegeSchoolName).HasMaxLength(100);
        builder.Property(e => e.CollegeAddress).HasMaxLength(100);
        builder.Property(e => e.CollegeGraduationDate).HasColumnType("date");
        builder.Property(e => e.CollegeDiplomaFileKey).HasMaxLength(100);
        builder.Property(e => e.CollegeDegree).HasMaxLength(100);
        builder.Property(e => e.CollegeMajor).HasMaxLength(100);
        builder.Property(e => e.BachelorsSchoolName).HasMaxLength(100);
        builder.Property(e => e.BachelorsAddress).HasMaxLength(100);
        builder.Property(e => e.BachelorsGraduationDate).HasColumnType("date");
        builder.Property(e => e.BachelorsDiplomaFileKey).HasMaxLength(100);
        builder.Property(e => e.BachelorsDegree).HasMaxLength(100);
        builder.Property(e => e.BachelorsMajor).HasMaxLength(100);
        builder.Property(e => e.MastersSchoolName).HasMaxLength(100);
        builder.Property(e => e.MastersAddress).HasMaxLength(100);
        builder.Property(e => e.MastersGraduationDate).HasColumnType("date");
        builder.Property(e => e.MastersDiplomaFileKey).HasMaxLength(100);
        builder.Property(e => e.MastersDegree).HasMaxLength(100);
        builder.Property(e => e.MastersMajor).HasMaxLength(100);
        builder.Property(e => e.PhDSchoolName).HasMaxLength(100);
        builder.Property(e => e.DoctorateAddress).HasMaxLength(100);
        builder.Property(e => e.DoctorateGraduationDate).HasColumnType("date");
        builder.Property(e => e.DoctorateDiplomaFileKey).HasMaxLength(100);
        builder.Property(e => e.DoctorateDegree).HasMaxLength(100);
        builder.Property(e => e.DoctorateMajor).HasMaxLength(100);

        builder.Property(e => e.SchoolSpecificLOAFileKey)
               .IsRequired(false);

        builder.Property(e => e.CreatedDate)
               .IsRequired(true);

        // Relationship to EmailInvitationRequest
        builder.HasOne<EmailInvitationRequest>()
               .WithOne(e => e.EducationalBackground)
               .HasForeignKey<EducationalBackground>(eb => eb.EmailInvitationID)
               .OnDelete(DeleteBehavior.Cascade)
               .IsRequired();
    }
}
