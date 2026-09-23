namespace EmploymentVerification.Data.EntityConfiguration;

public sealed class EmploymentVerificationContactConfiguration
	: IEntityTypeConfiguration<EmploymentVerificationContact>
{
	public void Configure(
		EntityTypeBuilder<EmploymentVerificationContact> builder)
	{
		builder.ToTable(
			"EmploymentVerificationContacts",
			"employment_verification");

		builder.HasKey(contact => contact.Id);

		// Longest company name in the seed data is 93 characters; the headroom is for
		// edits, not for the import.
		builder.Property(contact => contact.CompanyName)
			.HasMaxLength(150)
			.IsRequired();

		// Matches EmploymentVerificationRequest.HrEmail, which is the RFC 5321 ceiling.
		builder.Property(contact => contact.EmailAddress)
			.HasMaxLength(320)
			.IsRequired();

		// IsActive deliberately carries no HasDefaultValue: a database default without
		// a matching .ValueGeneratedOnAdd() makes the model snapshot disagree with the
		// model and trips PendingModelChangesWarning. The C# property initialiser on
		// the entity is the default instead.

		// Keyset seek index. Must stay in step with the ORDER BY in
		// ContactDirectoryRepository.GetContactsPageAsync - company name alone is not
		// unique (one company has 180 mailboxes), so Id breaks the tie.
		builder.HasIndex(contact => new
		{
			contact.CompanyName,
			contact.Id
		});

		// The business invariant: one row per (company, mailbox). Not a unique index on
		// EmailAddress alone - a shared HR inbox serving several subsidiaries is
		// legitimate here, and the imported extract having no duplicate emails is a
		// property of that one spreadsheet rather than a rule.
		//
		// Case-insensitivity depends on ContactDirectoryService normalising the email to
		// lower case before it reaches the repository; there is no citext column and no
		// functional index, because a lower() index cannot be expressed in the model and
		// would leave the snapshot no longer describing the database.
		builder.HasIndex(contact => new
		{
			contact.CompanyName,
			contact.EmailAddress
		})
			.IsUnique()
			.HasDatabaseName("UX_EmploymentVerificationContacts_Company_Email");
	}
}
