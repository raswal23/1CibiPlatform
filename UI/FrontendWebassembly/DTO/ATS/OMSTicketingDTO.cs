namespace FrontendWebassembly.DTO.ATS;

public record TicketedOrderListDTO
{
	public Guid EmailInvitationID { get; set; }

	public string? FirstName { get; set; }

	public string? MiddleInitial { get; set; }

	public string? LastName { get; set; }

	public string? EmailAddress { get; set; }

	public string? Requestor { get; set; }

	public string? SelectPackage { get; set; }

	public string? TicketStatus { get; set; }

	// Null until OMS returns a ticket for the order.
	public string? TicketNumber { get; set; }

	public DateTime? TicketDeliveryDate { get; set; }

	public int TicketAttempts { get; set; }

	// Why the last attempt failed; rendered beside an Error row.
	public string? TicketError { get; set; }

	public DateTime? OrderCreatedAt { get; set; }
}

public record TicketStatusCountsDTO
{
	public long Pending { get; set; }

	public long Processing { get; set; }

	public long Done { get; set; }

	public long Error { get; set; }

	public long Total { get; set; }
}

// Response envelopes, matching the property names the Carter endpoints return.
public record GetTicketedOrdersResponseDTO
{
	public KeysetPaginatedResult<TicketedOrderListDTO>? TicketedOrders { get; set; }
}

public record GetTicketStatusCountsResponseDTO
{
	public TicketStatusCountsDTO? Counts { get; set; }
}

// Mirrors ATS.Constants.OrderType. The server rejects anything else, so these are the
// only two values the UI may submit.
public static class OrderSpeed
{
	public const string Normal = "Normal";

	public const string Rush = "Rush";
}

// Mirrors ATS.Constants.TicketStatus, which lives in the backend assembly and is not
// referenced by the UI project.
public static class OrderTicketStatus
{
	public const string Pending = "Pending";

	public const string Processing = "Processing";

	public const string Done = "Done";

	public const string Error = "Error";

	// Mirrors OMSTicketingRepository.MaxTicketAttempts. Once an order has used this many
	// automatic attempts the job stops picking it up, which is when a person may retry
	// it by hand.
	public const int MaxAttempts = 5;
}
