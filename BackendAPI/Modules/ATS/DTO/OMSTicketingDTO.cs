namespace ATS.Data.DTO;

// Everything the OMS payload needs for one claimed order, assembled by the repository
// in a single round trip. PersonalDetails is a left join on purpose: an order is
// ticketed at enrolment, long before the applicant fills in the form, so DOB/SSS/TIN
// are legitimately absent and must not filter the row out.
public record TicketablePayloadDTO
{
	public Guid EmailInvitationID { get; set; }

	public string? FirstName { get; set; }

	public string? MiddleInitial { get; set; }

	public string? LastName { get; set; }

	public string? EmailAddress { get; set; }

	public string? MobileNumber { get; set; }

	public string? SelectPackage { get; set; }

	public Guid? RequestorId { get; set; }

	// From PersonalDetails when the application form has already been submitted.
	public DateOnly? DOB { get; set; }

	public string? PersonalMobileNumber { get; set; }

	public string? SSS { get; set; }

	public string? TIN { get; set; }

	// The numeric OMS report type is stored in the package description.
	public string? PackageDescription { get; set; }

	// From the requestor's ATS UserDetails row.
	public string? Site { get; set; }

	public string? RequestorFirstName { get; set; }

	public string? RequestorLastName { get; set; }

	public string? RequestorEmail { get; set; }
}

// Repository projection for the ticketing status screen. OrderCreatedAt and
// EmailInvitationID are the keyset sort keys, so both survive the projection.
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

	public string? TicketNumber { get; set; }

	public DateTime? TicketDeliveryDate { get; set; }

	public int TicketAttempts { get; set; }

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
