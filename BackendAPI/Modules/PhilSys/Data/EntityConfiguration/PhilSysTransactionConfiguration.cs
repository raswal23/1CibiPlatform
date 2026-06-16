namespace PhilSys.Data.EntityConfiguration;

public class PhilSysTransactionConfiguration : IEntityTypeConfiguration<PhilSysTransaction>
{
	public void Configure(EntityTypeBuilder<PhilSysTransaction> builder)
	{
		builder.ToTable("PhilSysTransaction", "philsys");

		builder.HasKey(pt => pt.Tid);

		builder.Property(pt => pt.Tid)
			   .IsRequired();

		builder.Property(pt => pt.Tid)
		       .ValueGeneratedNever();

		builder.Property(pt => pt.InquiryType)
			   .IsRequired()
		   	   .HasMaxLength(10);

		builder.Property(pt => pt.FirstName)
			   .HasMaxLength(100);

		builder.Property(pt => pt.LastName)
			   .HasMaxLength(100);

		builder.Property(pt => pt.MiddleName)
			   .HasMaxLength(100);

		builder.Property(pt => pt.Suffix)
			   .HasMaxLength(20);

		builder.Property(pt => pt.BirthDate)
			   .HasMaxLength(10);

		builder.Property(pt => pt.PCN)
			   .HasMaxLength(16);

		builder.Property(pt => pt.FaceLivenessSessionId)
			   .HasMaxLength(100);

		builder.Property(pt => pt.WebHookUrl)
			   .HasMaxLength(255);

		builder.Property(pt => pt.IsTransacted)
			   .HasDefaultValue(false);

		builder.Property(pt => pt.HashToken)
			   .IsRequired()
			   .HasMaxLength(100);

		builder.Property(pt => pt.ATSSession)
			   .IsRequired()
			   .HasMaxLength(100);

		builder.Property(builder => builder.UpdatedLivenessIdAt)
			   .IsRequired(false);

		builder.Property(pt => pt.ExpiresAt)
			   .IsRequired();

		builder.Property(pt => pt.CreatedAt)
			   .IsRequired()
			   .HasDefaultValueSql("timezone('utc', now())");

		builder.Property(builder => builder.TransactedAt)
			   .IsRequired(false);
	}
}
