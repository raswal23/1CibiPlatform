namespace ATS.Data.EntityConfiguration;

public class EmailProcessDetailsConfiguration : IEntityTypeConfiguration<EmailProcessDetails>
{
	public void Configure(EntityTypeBuilder<EmailProcessDetails> builder)
	{
		builder.ToTable("EmailProcessDetails", "ats");

		builder.HasKey(x => x.Id);

		builder.Property(x => x.Id)
			   .ValueGeneratedOnAdd();

		// 40 matches AtsEmailAccount.VerificationStatus, the sibling column that also stores a
		// constant's string name.
		builder.Property(x => x.EmailProcess)
			   .HasMaxLength(40)
			   .IsRequired();

		// Holds a comma-separated LIST, not one address, so it is sized for several: four
		// mailboxes at the 255 of AtsEmailAccount.EmailAddress, plus their delimiters, rounded
		// to 1000 - the same width AtsEmailAccountOtp.PendingChangesJson uses for its own
		// multi-value column.
		//
		// NOT NULL, which EF infers from the non-nullable property - IsRequired() would say the
		// same thing and is left off as redundant rather than as a relaxation. NOT NULL is
		// wanted here: "nobody is copied on this notice" is the EMPTY STRING, so a reader never
		// has to treat null and "" as the same thing. Note that neither NOT NULL nor
		// IsRequired() rejects an empty value - only a validator can.
		builder.Property(x => x.CCEmail)
			   .HasMaxLength(1000);

		builder.Property(x => x.CreatedDate)
			   .IsRequired();

		builder.Property(x => x.IsActive)
			   .IsRequired();

		// One row per notice, so the process alone is unique. This is what makes the list the
		// unit of editing: a second row for a process would give one notice two copy lists and
		// nothing would say which one the send path should read.
		//
		// Note what this canNOT enforce: the index constrains the column, never the addresses
		// inside it. A list repeating one mailbox is a distinct string and passes. See the
		// remarks on the entity.
		builder.HasIndex(x => x.EmailProcess)
			   .IsUnique();
	}
}
