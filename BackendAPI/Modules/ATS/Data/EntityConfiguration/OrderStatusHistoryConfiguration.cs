namespace ATS.Data.EntityConfiguration;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
	public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
	{
		builder.ToTable("OrderStatusHistory", "ats");
		builder.HasKey(x => x.OrderStatusHistoryId);
		builder.Property(x => x.OrderStatusHistoryId).ValueGeneratedNever();
		builder.Property(x => x.EventType).HasMaxLength(80).IsRequired();
		builder.Property(x => x.PreviousStatus).HasMaxLength(255);
		builder.Property(x => x.NewStatus).HasMaxLength(255).IsRequired();
		builder.Property(x => x.Source).HasMaxLength(40).IsRequired();
		builder.Property(x => x.OccurredAt).IsRequired();
		builder.HasIndex(x => new { x.EmailInvitationRequestId, x.OccurredAt });
		builder.HasOne(x => x.EmailInvitationRequest)
			.WithMany(x => x.OrderStatusHistories)
			.HasForeignKey(x => x.EmailInvitationRequestId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
