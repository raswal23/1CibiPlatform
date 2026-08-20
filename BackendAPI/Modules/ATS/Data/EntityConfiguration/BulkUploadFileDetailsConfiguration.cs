namespace ATS.Data.EntityConfiguration;

public class BulkUploadFileDetailsConfiguration : IEntityTypeConfiguration<BulkUploadFileDetails>
{
	public void Configure(EntityTypeBuilder<BulkUploadFileDetails> builder)
	{
		builder.ToTable("BulkUploadFileDetails", "ats");

		builder.HasKey(a => a.FileID);

		builder.Property(a => a.FileID)
			   .IsRequired()
			   .ValueGeneratedNever();

		builder.Property(a => a.UploadedByUserId)
			   .IsRequired();

		// Carried to every invitation the bulk job creates from this file.
		builder.Property(a => a.Requestor)
			   .HasMaxLength(255)
			   .IsRequired(false);

		builder.Property(a => a.FileName)
			   .IsRequired()
			   .HasMaxLength(255);

		builder.Property(a => a.FileKey)
			   .IsRequired()
			   .HasMaxLength(255);

		builder.Property(a => a.PackageType)
			   .IsRequired()
			   .HasMaxLength(255);

		builder.Property(a => a.OrderType)
			   .IsRequired()
			   .HasMaxLength(50);

		builder.Property(e => e.ClientId);

		builder.Property(a => a.Status)
			   .IsRequired()
			   .HasMaxLength(50);

		builder.Property(a => a.ClaimedAt)
			   .IsRequired(false);

		builder.Property(a => a.DateCreated)
			   .IsRequired();

		// Drives the bulk submission job's claim query and the stale-claim sweeper.
		builder.HasIndex(a => a.Status);
	}
}
