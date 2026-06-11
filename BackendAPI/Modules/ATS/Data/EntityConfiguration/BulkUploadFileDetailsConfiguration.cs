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

		builder.Property(a => a.FileName)
			   .IsRequired()
			   .HasMaxLength(255);

		builder.Property(a => a.FileKey)
			   .IsRequired()
			   .HasMaxLength(255);

		builder.Property(a => a.Status)
			   .IsRequired()
			   .HasMaxLength(50);

		builder.Property(a => a.DateCreated)
			   .IsRequired();
	}
}
