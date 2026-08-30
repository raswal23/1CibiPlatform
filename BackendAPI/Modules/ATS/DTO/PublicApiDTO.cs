namespace ATS.Data.DTO;

// One order as an integrating client sees it: where it is in the pipeline, its OMS
// ticket once raised, and the events that got it there.
public record PublicOrderDetailDTO
{
	public Guid OrderId { get; set; }

	public string? FirstName { get; set; }

	public string? MiddleInitial { get; set; }

	public string? LastName { get; set; }

	public string? EmailAddress { get; set; }

	public string? MobileNumber { get; set; }

	public string? Package { get; set; }

	public string? OrderType { get; set; }

	public string? OrderStatus { get; set; }

	public string? ApplicationFormStatus { get; set; }

	public string? TicketNumber { get; set; }

	public DateTime? TicketDeliveryDate { get; set; }

	public DateTime? OrderCreatedAt { get; set; }

	public DateTime? FormCompletedAt { get; set; }

	public DateTime? OrderCompletedAt { get; set; }

	public IReadOnlyList<OrderStatusHistoryDTO> History { get; set; } = [];
}

// The parse outcome of an uploaded CSV. Written once by the background job, read back
// on demand - the upload response returns long before the file has been parsed.
public record PublicBulkUploadStatusDTO
{
	public Guid FileId { get; set; }

	public string? FileName { get; set; }

	public string? Status { get; set; }

	public string? Package { get; set; }

	public string? OrderType { get; set; }

	public DateTime DateCreated { get; set; }

	public int AcceptedRowCount { get; set; }

	public int RejectedRowCount { get; set; }

	public IReadOnlyList<BulkUploadRejectedRowDTO> RejectedRows { get; set; } = [];
}
