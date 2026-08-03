namespace ATS.Data.EntityConfiguration;

public class ArchiveReportConfiguration : IEntityTypeConfiguration<ArchiveReport>
{
	public void Configure(EntityTypeBuilder<ArchiveReport> builder)
	{
		builder.ToTable("ArchiveReport", "ats");

		builder.HasKey(x => x.ArchiveReportId);

		builder.Property(x => x.ArchiveReportId)
			.IsRequired()
			.ValueGeneratedNever();

		builder.Property(x => x.EmailInvitationRequestId)
			.IsRequired();

		builder.Property(x => x.ReportStatus)
			.HasMaxLength(255)
			.IsRequired();

		builder.Property(x => x.ReportFileName)
			.HasMaxLength(525)
			.IsRequired();

		builder.Property(x => x.ReportFileKey)
			.HasMaxLength(500)
			.IsRequired();

		builder.Property(x => x.ReportUploadedAt)
			.IsRequired();

		builder.HasOne(x => x.EmailInvitationRequest)
			.WithMany(x => x.ArchiveReports)
			.HasForeignKey(x => x.EmailInvitationRequestId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired();
	}
}
