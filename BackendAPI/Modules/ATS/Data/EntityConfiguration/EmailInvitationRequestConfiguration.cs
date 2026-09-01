namespace ATS.Data.EntityConfiguration;

public class EmailInvitationRequestConfiguration : IEntityTypeConfiguration<EmailInvitationRequest>
{
	public void Configure(EntityTypeBuilder<EmailInvitationRequest> builder)
	{
		builder.ToTable("EmailInvitationRequest", "ats");

		builder.HasKey(e => e.EmailInvitationID);

		builder.Property(e => e.EmailInvitationID)
			   .IsRequired()
			   .ValueGeneratedNever();

		builder.Property(e => e.LastName)
			   .HasMaxLength(255)
			   .IsRequired(true);

		builder.Property(e => e.FirstName)
			   .HasMaxLength(255)
			   .IsRequired(true);

		builder.Property(e => e.MiddleInitial)
			   .HasMaxLength(255);

		builder.Property(e => e.EmailAddress)
			   .HasMaxLength(255)
			   .IsRequired(true);

		builder.Property(e => e.MobileNumber)
			   .HasMaxLength(255)
			   .IsRequired(true);

		builder.Property(e => e.Requestor)
			   .HasMaxLength(255)
			   .IsRequired(false);

		// The real relationship. Restrict, not Cascade: removing a package must never
		// delete the orders placed under it.
		builder.Property(e => e.PackageId)
			   .IsRequired();

		builder.HasOne<PackageDetails>()
			   .WithMany()
			   .HasForeignKey(e => e.PackageId)
			   .OnDelete(DeleteBehavior.Restrict);

		// Denormalised label. Kept because the report lists, search, exports and the
		// ticketing screen all read the name directly; PackageId is what OMS ticketing
		// resolves against, so a rename can no longer orphan an order.
		builder.Property(e => e.SelectPackage)
			   .HasMaxLength(255)
			   .IsRequired(true);

		builder.Property(e => e.RushNormal)
			   .HasMaxLength(255)
			   .IsRequired(true);

		builder.Property(e => e.HashToken)
			   .HasMaxLength(255)
			   .IsRequired(true);

		builder.Property(e => e.ClientId)
			   .IsRequired(false);

		// The endorsement and bulk flows create invitations without a requestor id, so
		// it stays optional and is only populated when the requestor can be resolved.
		builder.Property(e => e.RequestorId)
			   .IsRequired(false);

		builder.Property(e => e.HashTokenCreatedAt)
			   .IsRequired(true);

		builder.Property(e => e.HashTokenExpiration)
			   .IsRequired(true);

		builder.Property(e => e.EmailSentStatus)
			   .HasMaxLength(255)
			   .IsRequired(true);

		builder.Property(e => e.ApplicationFormStatus)
			   .HasMaxLength(255)
			   .IsRequired(true);

		builder.Property(e => e.OrderStatus)
			   .HasMaxLength(255)
			   .IsRequired(true);

		builder.Property(e => e.OrderCreatedAt)
			   .IsRequired(false);

		builder.Property(e => e.NeedsProjection)
			   .IsRequired(true)
			   .HasDefaultValue(true);

		builder.Property(e => e.ProjectionUpdatedAt)
			   .IsRequired(false);

		builder.Property(e => e.DisputeCategory)
			   .HasMaxLength(255)
			   .IsRequired(false);

		builder.Property(e => e.DisputedAt)
			   .IsRequired(false);

		// Composite indexes matching the keyset (seek) pagination orderings; each seek
		// tuple needs an index with the same columns and directions to avoid full scans.
		builder.HasIndex(e => new { e.OrderCompletedAt, e.EmailInvitationID })
			   .IsDescending(true, false);
		builder.HasIndex(e => new { e.FirstName, e.LastName, e.EmailInvitationID });
		builder.HasIndex(e => new { e.OrderStatus, e.EmailInvitationID });
		builder.HasIndex(e => new { e.OrderCreatedAt, e.EmailInvitationID })
			   .IsDescending(true, false);

		builder.Property(e => e.EmailClaimedAt)
			   .IsRequired(false);

		builder.Property(e => e.EmailSendAttempts)
			   .IsRequired(true)
			   .HasDefaultValue(0);

		// Null for single inquiries; set to the source file for bulk uploads.
		builder.Property(e => e.BulkFileID)
			   .IsRequired(false);

		// Drives the email notification job's claim query and the stale-claim sweeper.
		builder.HasIndex(e => e.EmailSentStatus);

		builder.Property(e => e.TicketStatus)
			   .HasMaxLength(50)
			   .IsRequired(false);

		builder.Property(e => e.IsTicketed)
			   .IsRequired(true)
			   .HasDefaultValue(false);

		builder.Property(e => e.TicketNumber)
			   .HasMaxLength(100)
			   .IsRequired(false);

		builder.Property(e => e.TicketDeliveryDate)
			   .IsRequired(false);

		builder.Property(e => e.TicketClaimedAt)
			   .IsRequired(false);

		builder.Property(e => e.TicketAttempts)
			   .IsRequired(true)
			   .HasDefaultValue(0);

		builder.Property(e => e.TicketError)
			   .HasMaxLength(500)
			   .IsRequired(false);

		// Drives the OMS ticketing job's claim query and the stale-claim sweeper, the
		// same role EmailSentStatus plays for the email queue.
		builder.HasIndex(e => e.TicketStatus);

		// Traces every invitation back to the bulk file it came from, and matches the
		// drill-down's (BulkFileID, EmailInvitationID ASC) keyset walk. It replaces the
		// former single-column BulkFileID index rather than sitting beside it: the
		// leading column still serves the rollup's WHERE BulkFileID IN (...), and this
		// table is write-hot enough that a redundant index is pure cost.
		builder.HasIndex(e => new { e.BulkFileID, e.EmailInvitationID });
	}
}
