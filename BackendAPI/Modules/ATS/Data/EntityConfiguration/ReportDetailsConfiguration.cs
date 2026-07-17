namespace ATS.Data.EntityConfiguration;

public class ReportDetailsConfiguration : IEntityTypeConfiguration<ReportDetails>
{
	public void Configure(EntityTypeBuilder<ReportDetails> builder)
	{
		builder.ToTable("ReportDetails", "ats");

		builder.HasKey(x => x.ReportFileId);

		builder.Property(x => x.ReportFileId)
			.IsRequired()
			.ValueGeneratedNever();

		builder.Property(x => x.EmailInvitationRequestId)
			.IsRequired();

		builder.Property(x => x.HitStatus)
			.HasMaxLength(255)
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
			.WithMany(x => x.ReportDetails)
			.HasForeignKey(x => x.EmailInvitationRequestId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired();

		builder.HasIndex(x => new { x.EmailInvitationRequestId, x.ReportStatus })
			.IsUnique();
	}
}
