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

		builder.Property(a => a.Source)
			   .HasMaxLength(40)
			   .IsRequired(false);

		builder.Property(a => a.AcceptedRowCount)
			   .IsRequired()
			   .HasDefaultValue(0);

		builder.Property(a => a.RejectedRowCount)
			   .IsRequired()
			   .HasDefaultValue(0);

		// A JSON array of {row, reason}. Unbounded rather than capped: a large file with
		// many bad rows must still record all of them, and this is read only on demand.
		builder.Property(a => a.RejectedRows)
			   .IsRequired(false);

		// Drives the bulk submission job's claim query and the stale-claim sweeper.
		builder.HasIndex(a => a.Status);

		// The dashboard's unfiltered keyset walk: (DateCreated DESC, FileID ASC).
		builder.HasIndex(a => new { a.DateCreated, a.FileID })
			   .IsDescending(true, false);

		// The same walk with a status chip selected.
		builder.HasIndex(a => new { a.Status, a.DateCreated, a.FileID })
			   .IsDescending(false, true, false);
	}
}
